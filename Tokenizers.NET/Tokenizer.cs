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

    public unsafe struct Tokenizer<ConfigT>: IDisposable
        where ConfigT: struct, ITokenizerConfig
    {
        private struct TempFixedAllocator: IDisposable
        {
            public static readonly int BUFFER_SIZE = Encoding.UTF8.GetMaxByteCount(ConfigT.ExpectedMaxInputLength.ToSignedUnchecked());
            
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
                
                if (Buffers.Length == 0)
                {
                    throw new InvalidOperationException("Buffers length cannot be zero.");
                }
            }

            public ref struct Handle
                #if NET9_0_OR_GREATER
                : IDisposable
                #endif
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] GetFirstAllocationUnsafely()
            {
                return MemoryMarshal.GetArrayDataReference(Buffers);
            } 

            public void Dispose()
            {
                Buffers = null;
            }
        }

        private static readonly bool TRUNCATE;
        
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
        
        public bool Truncate => TRUNCATE;
        
        public Tokenizer()
        {
            var tokenizerData = TOKENIZER_DATA;
            
            Allocator = new();
            
            TokenizerHandle = TokenizerNativeMethods.AllocateTokenizer(
                tokenizerData.PinnedArrayToPointer(), 
                (nuint) tokenizerData.Length);
        }
        
        public void Tokenize(ReadOnlySpan<string> inputs, Span<TokenizeOutput> outputs)
        {
            TokenizeInternal(
                inputs, 
                outputs, 
                outputsPrePinned: false,
                skipLengthCheck: false
            );
        }
        
        [SkipLocalsInit]
        public TokenizeOutput Tokenize(string input)
        {
            Span<byte> allocation;

            var inputLength = input.Length;

            Unsafe.SkipInit(out NativeMemory<byte> nativeMemory);
            
            var useNativeMemory = inputLength > ConfigT.ExpectedMaxInputLength;
            
            if (!useNativeMemory)
            {
                const int MAX_STACK_ALLOC = 4096;
                
                // This branch is free
                if (MAX_STACK_ALLOC >= TempFixedAllocator.BUFFER_SIZE)
                {
                    // A result of a stackalloc expression of this type in this context may be exposed outside of the containing method
                    #pragma warning disable CS9081
                    allocation = stackalloc byte[MAX_STACK_ALLOC];
                    #pragma warning restore CS9081
                }

                else
                {
                    allocation = Allocator.GetFirstAllocationUnsafely();
                }
            }

            else
            {
                nativeMemory = new((nuint) Encoding.UTF8.GetMaxByteCount(inputLength));
                        
                allocation = nativeMemory.Buffer.AsSpan();
            }

            var bytesWritten = Encoding.UTF8.GetBytes(input, allocation);
            
            var u8String = new ReadOnlyNativeBuffer<byte>(ref MemoryMarshal.GetReference(allocation), (nuint) bytesWritten);
            
            var result = TokenizerNativeMethods.TokenizerEncode(TokenizerHandle, u8String, TRUNCATE);
            
            if (useNativeMemory)
            {
                nativeMemory.Dispose();
            }
            
            return result;
        }
        
        public void Tokenize(ReadOnlySpan<string> inputs, NativeMemory<TokenizeOutput> outputs)
        {
            TokenizeInternal(
                inputs, 
                outputs.Buffer.AsSpan(), 
                outputsPrePinned: true,
                skipLengthCheck: false
            );
        }
        
        public NativeMemory<TokenizeOutput> Tokenize(ReadOnlySpan<string> inputs)
        {
            var outputs = new NativeMemory<TokenizeOutput>((nuint) inputs.Length);
            
            TokenizeInternal(
                inputs, 
                outputs.Buffer.AsSpan(), 
                outputsPrePinned: true,
                skipLengthCheck: true
            );
            
            return outputs;
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                        
                        allocation = nativeMemory.Buffer.AsSpan();
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

            var truncate = TRUNCATE;
            
            if (outputsPrePinned)
            {
                TokenizerNativeMethods.TokenizerEncodeBatch(
                    tokenizerPtr: tokenizerHandle,
                    textNativeBuffers: readonlyU8Strings,
                    outputNativeBuffer: new(ref outputStart, outputLengthNative),
                    truncate
                );
            }

            else
            {
                fixed(TokenizeOutput* outputsPtr = &outputStart)
                {
                    TokenizerNativeMethods.TokenizerEncodeBatch(
                        tokenizerPtr: tokenizerHandle,
                        textNativeBuffers: readonlyU8Strings,
                        outputNativeBuffer: new(outputsPtr, outputLengthNative),
                        truncate
                    );
                }
            }
            
            foreach (var nativeMemory in nativeAllocations.AsSpan())
            {
                nativeMemory.Dispose();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput Decode(ReadOnlySpan<uint> ids, bool skipSpecialTokens)
        {
            ref var first = ref MemoryMarshal.GetReference(ids);
            
            fixed(uint* ptr = &first)
            {
                return Decode((ReadOnlyNativeBuffer<uint>) new(ptr, (nuint) ids.Length), skipSpecialTokens);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput Decode(ReadOnlyNativeBuffer<uint> ids, bool skipSpecialTokens)
        {
            var tokenizerHandle = TokenizerHandle;
            
            return TokenizerNativeMethods.TokenizerDecode(tokenizerHandle, ids, skipSpecialTokens);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput DecodeMutating(Span<ulong> ids, bool skipSpecialTokens)
        {
            fixed (ulong* ptr = &MemoryMarshal.GetReference(ids))
            {
                var tokenizerHandle = TokenizerHandle;
                
                var idsBuffer = new NativeBuffer<ulong>(ptr, (nuint) ids.Length);
                
                return DecodeMutating(idsBuffer, skipSpecialTokens);
            }
        }
        
        public DecodeOutput DecodeMutating(NativeBuffer<ulong> ids, bool skipSpecialTokens)
        {
            var tokenizerHandle = TokenizerHandle;

            var mutated = ids.NarrowMutating().AsReadOnly();
            
            // The length should still be the same, even though the actual underlying length is double
            mutated = new(mutated.Ptr, ids.Length);
            
            return TokenizerNativeMethods.TokenizerDecode(
                tokenizerHandle, 
                mutated,
                skipSpecialTokens
            );
        }
        
        public void Dispose()
        {
            Allocator.Dispose();
            TokenizerNativeMethods.FreeTokenizer(TokenizerHandle);
        }
    }
}