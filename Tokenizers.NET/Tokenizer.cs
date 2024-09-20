using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Tokenizers.NET.Collections;
using Tokenizers.NET.Helpers;

namespace Tokenizers.NET
{
    public interface ITokenizerConfig
    {
        public static abstract uint ExpectedMaxInputLength { get; }
        
        public static abstract uint ExpectedMaxBatches { get; }
        
        public static abstract string TokenizerJsonPath { get; }
    }

    public unsafe partial struct Tokenizer<ConfigT>: IDisposable
        where ConfigT: struct, ITokenizerConfig
    {
        private static readonly bool TRUNCATE;
        
        private struct TempFixedAllocator: IDisposable
        {
            private static readonly int BUFFER_SIZE = Encoding.UTF8.GetMaxByteCount(ConfigT.ExpectedMaxInputLength.ToSignedUnchecked());
            
            private byte[][] Buffers;

            private int Count;

            public TempFixedAllocator()
            {
                var maxExpectedBatches = ConfigT.ExpectedMaxBatches;
                
                var buffers = Buffers = AllocationHelpers.AllocatePinnedUninitialized<byte[]>(maxExpectedBatches);
                Count = maxExpectedBatches.ToSignedUnchecked();
                
                for (var i = 0; i < maxExpectedBatches; i++)
                {
                    buffers[i] = AllocationHelpers.AllocatePinnedUninitialized<byte>(BUFFER_SIZE);
                }
            }

            public ref struct Handle: IDisposable
            {
                private readonly ref TempFixedAllocator Allocator;

                internal int CurrentCount;
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal Handle(ref TempFixedAllocator allocator)
                {
                    Allocator = ref allocator;
                    CurrentCount = allocator.Count;
                }
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public byte[] Allocate()
                {
                    return Allocator.Allocate(ref this);
                }
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Dispose()
                {
                    #if DEBUG
                    ref var allocator = ref Allocator;
                    
                    var buffers = allocator.Buffers;

                    var count = allocator.Count;
                
                    Debug.Assert(buffers.AsSpan(0, count).ToArray().All(buffer => buffer != null));
                    Debug.Assert(buffers.AsSpan(count).ToArray().All(buffer => buffer == null));
                    #endif
                }   
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Handle GetHandle()
            {
                return new(ref this);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte[] Allocate(ref Handle handle)
            {
                var count = handle.CurrentCount;

                if (count != 0)
                {
                    var readIndex = handle.CurrentCount = count - 1;
                    
                    // Don't clear underlying reference, this is to ensure that the buffer will not be collected by GC.
                    // When p/invoking, we pass the pointer to the buffer, so we don't want it to be collected.
                    // It is also more performant to avoid clearing...
                    return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Buffers), readIndex);
                }
                
                return AllocateSlow();
            }
            
            [MethodImpl(MethodImplOptions.NoInlining)]
            private byte[] AllocateSlow()
            {
                var oldCountBeforeScope = Count++;
                
                var newAllocation = AllocationHelpers.AllocatePinnedUninitialized<byte>(BUFFER_SIZE);

                var oldBuffers = Buffers;
                var oldLength = oldBuffers.Length;
                
                // This is possible.
                // Imagine oldCountBeforeScope is length 5, but the underlying buffer is length 8
                if (oldCountBeforeScope != oldLength)
                {
                    oldBuffers[oldCountBeforeScope] = newAllocation;
                    return newAllocation;
                }

                var newBuffers = Buffers = AllocationHelpers.AllocatePinnedUninitialized<byte[]>(oldLength * 2);
                oldBuffers.AsSpan(0, oldLength).CopyTo(newBuffers);
                
                newBuffers[oldCountBeforeScope] = newAllocation;
                return newAllocation;
            }

            public void Dispose()
            {
                Buffers = null;
            }
        }

        private static readonly byte[] TOKENIZER_DATA;
        
        static Tokenizer()
        {
            var bytes = File.ReadAllBytes(ConfigT.TokenizerJsonPath);
            
            var tokenizerDataBytes = TOKENIZER_DATA = AllocationHelpers
                .AllocatePinnedUninitialized<byte>(bytes.Length);
            
            bytes.AsSpan().CopyTo(tokenizerDataBytes);

            var tokenizerData = JsonSerializer.Deserialize<TokenizerData>(tokenizerDataBytes);
            
            TRUNCATE = tokenizerData.Truncation != null;
        }
        
        private TempFixedAllocator Allocator;
        
        private readonly nint TokenizerHandle;
        
        public Tokenizer()
        {
            var tokenizerData = TOKENIZER_DATA;
            
            Allocator = new();
            
            TokenizerHandle = TokenizerNativeMethods.AllocateTokenizer(
                tokenizerData.PinnedArrayToPointer(), 
                (nuint) tokenizerData.Length);
        }
        
        [SkipLocalsInit]
        public void Tokenize(ReadOnlySpan<string> inputs, Span<TokenizeOutput> outputs)
        {
            TokenizeInternal(
                inputs, 
                outputs, 
                outputsPrePinned: false,
                skipLengthCheck: false
            );
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tokenize(ReadOnlySpan<string> inputs, NativeMemory<TokenizeOutput> outputs)
        {
            TokenizeInternal(
                inputs, 
                outputs.Memory.AsSpan(), 
                outputsPrePinned: true,
                skipLengthCheck: false
            );
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<TokenizeOutput> Tokenize(ReadOnlySpan<string> inputs)
        {
            var outputs = new NativeMemory<TokenizeOutput>((nuint) inputs.Length);
            
            TokenizeInternal(
                inputs, 
                outputs.Memory.AsSpan(), 
                outputsPrePinned: true,
                skipLengthCheck: true
            );
            
            return outputs;
        }

        [SkipLocalsInit]
        private void TokenizeInternal(
            ReadOnlySpan<string> inputs,
            Span<TokenizeOutput> outputs,
            bool outputsPrePinned,
            bool skipLengthCheck)
        {
            var numInputs = inputs.Length;
            
            if (!skipLengthCheck && numInputs != outputs.Length)
            {
                throw new ArgumentException("Number of inputs must be equal to the number of outputs.");
            }

            using var nativeAllocations = new StackList<NativeMemory<byte>>(
                stackalloc NativeMemory<byte>[numInputs < 32 ? numInputs : 32]
            );
            
            var u8Strings = new NativeBuffer<ReadOnlyNativeBuffer<byte>>(
                stackalloc ReadOnlyNativeBuffer<byte>[numInputs]
            );
            
            using var allocator = Allocator.GetHandle();

            if (true) // Just to make sure currentU8String is not accessible outside of this scope
            {
                var currentU8String = u8Strings.Ptr;
            
                foreach (var input in inputs)
                {
                    Span<byte> allocation;

                    var inputLength = (nuint) input.Length;
                    
                    if (inputLength <= ConfigT.ExpectedMaxInputLength)
                    {
                        allocation = allocator.Allocate();
                    }

                    else
                    {
                        var nativeMemory = new NativeMemory<byte>(inputLength);
                        nativeAllocations.Add(nativeMemory);
                        
                        allocation = nativeMemory.Memory.AsSpan();
                    }

                    Encoding.UTF8.TryGetBytes(input, allocation, out var bytesWritten);
                
                    *currentU8String = new(ref MemoryMarshal.GetReference(allocation), (nuint) bytesWritten);
                    
                    currentU8String++;
                }
            }
            
            var readonlyU8Strings = u8Strings.AsReadOnly();

            var tokenizerHandle = TokenizerHandle;
            
            var outputLengthNative = (nuint) outputs.Length;
            
            ref var outputStart = ref MemoryMarshal.GetReference(outputs);
            
            if (outputsPrePinned)
            {
                TokenizerNativeMethods.TokenizerEncodeBatch(
                    tokenizerPtr: tokenizerHandle,
                    textNativeBuffers: readonlyU8Strings,
                    outputNativeBuffer: new(ref outputStart, outputLengthNative)
                );
            }

            else
            {
                fixed(TokenizeOutput* outputsPtr = &outputStart)
                {
                    TokenizerNativeMethods.TokenizerEncodeBatch(
                        tokenizerPtr: tokenizerHandle,
                        textNativeBuffers: readonlyU8Strings,
                        outputNativeBuffer: new(outputsPtr, outputLengthNative)
                    );
                }
            }
            
            foreach (var nativeMemory in nativeAllocations.AsSpan())
            {
                nativeMemory.Dispose();
            }
        }
        
        public void Dispose()
        {
            Allocator.Dispose();
            TokenizerNativeMethods.FreeTokenizer(TokenizerHandle);
        }
    }
}