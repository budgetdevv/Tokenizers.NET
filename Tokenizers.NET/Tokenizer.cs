using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    public static unsafe class Tokenizer
    {
        public enum ExceedExpectedMaxBatchesBehavior
        {
            AllocateBuffer,
            AllocatePooledBuffer,
            // Discard, // TODO: Implement this
        }
        
        public interface IConfig
        {
            public static abstract BuiltConfig BuiltConfig { get; }
        }

        public struct ConfigBuilder
        {
            internal uint ExpectedMaxInputLength, ExpectedMaxBatches;

            internal ExceedExpectedMaxBatchesBehavior ExceedExpectedMaxBatchesBehavior;

            internal string? TokenizerJsonPath;

            internal byte[]? RawTokenizerData;

            public ConfigBuilder()
            {
                ExpectedMaxInputLength = 1024;

                ExpectedMaxBatches = 16;

                ExceedExpectedMaxBatchesBehavior = ExceedExpectedMaxBatchesBehavior.AllocateBuffer;

                TokenizerJsonPath = null;

                RawTokenizerData = null;
            }
            
            [UnscopedRef]
            public ref ConfigBuilder SetExpectedMaxInputLength(uint expectedMaxInputLength)
            {
                ExpectedMaxInputLength = expectedMaxInputLength;
                return ref this;
            }
            
            [UnscopedRef]
            public ref ConfigBuilder SetExpectedMaxBatches(uint expectedMaxBatches)
            {
                ExpectedMaxBatches = expectedMaxBatches;
                return ref this;
            }
            
            [UnscopedRef]
            public ref ConfigBuilder SetExceedExpectedMaxBatchesBehavior(ExceedExpectedMaxBatchesBehavior exceedExpectedMaxBatchesBehavior)
            {
                ExceedExpectedMaxBatchesBehavior = exceedExpectedMaxBatchesBehavior;
                return ref this;
            }
            
            [UnscopedRef]
            public ref ConfigBuilder SetTokenizerJsonPath(string tokenizerJsonPath)
            {
                TokenizerJsonPath = tokenizerJsonPath;
                return ref this;
            }
            
            [UnscopedRef]
            public ref ConfigBuilder SetRawTokenizerData(byte[] rawTokenizerData)
            {
                RawTokenizerData = rawTokenizerData;
                return ref this;
            }
            
            public BuiltConfig Build()
            {
                return new(this);
            }
        }

        public readonly struct BuiltConfig
        {
            public readonly uint ExpectedMaxInputLength, ExpectedMaxBatches;
        
            public readonly ExceedExpectedMaxBatchesBehavior ExceedExpectedMaxBatchesBehavior;
        
            public readonly string? TokenizerJsonPath;

            public readonly NativeMemory<byte> RawTokenizerData;

            public readonly bool Truncates;

            [Obsolete("Use constructor with parameters.", error: true)]
            public BuiltConfig()
            {
                throw new NotSupportedException("Use constructor with parameters.");
            }
            
            internal BuiltConfig(ConfigBuilder builder)
            {
                ExpectedMaxInputLength = builder.ExpectedMaxInputLength;
                ExpectedMaxBatches = builder.ExpectedMaxBatches;
                ExceedExpectedMaxBatchesBehavior = builder.ExceedExpectedMaxBatchesBehavior;
                
                var tokenizerJsonPath = TokenizerJsonPath = builder.TokenizerJsonPath;
                
                var rawTokenizerDataArr = builder.RawTokenizerData;

                // Let it throw if both are null
                rawTokenizerDataArr ??= File.ReadAllBytes(tokenizerJsonPath!);

                // TODO: Consider mmap instead of heap allocation.
                
                var rawTokenizerData = RawTokenizerData = new((nuint) rawTokenizerDataArr.Length);

                var rawTokenizerDataSpan = rawTokenizerData.Buffer.AsSpan();
                
                rawTokenizerDataArr.CopyTo(rawTokenizerDataSpan);
                
                var tokenizerData = JsonSerializer.Deserialize<TokenizerData>(rawTokenizerDataSpan);
            
                Truncates = tokenizerData.Truncation != null;
            }
        }
    }
    
    public unsafe struct Tokenizer<ConfigT>: IDisposable
        where ConfigT: struct, Tokenizer.IConfig
    {
        private struct TempFixedAllocator: IDisposable
        {
            public static readonly int BUFFER_SIZE = Encoding.UTF8.GetMaxByteCount(ConfigT.BuiltConfig.ExpectedMaxInputLength.ToSignedUnchecked());
            
            private byte[][] Buffers;

            private int Count;

            public TempFixedAllocator()
            {
                var config = ConfigT.BuiltConfig;
                
                var maxExpectedBatches = config.ExpectedMaxBatches;
                
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
                
                var config = ConfigT.BuiltConfig;

                // This check is free if ConfigT.ExceedExpectedMaxBatchesBehavior == ExceedExpectedMaxBatchesBehavior.AllocateBuffer,
                // which in turn eliminates AllocateSlow() call.
                if (count == 0 || config.ExceedExpectedMaxBatchesBehavior == Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
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
        
        private TempFixedAllocator Allocator;
        
        private readonly nint TokenizerHandle;
        
        public Tokenizer.BuiltConfig Config => ConfigT.BuiltConfig;
        
        public bool Truncate => ConfigT.BuiltConfig.Truncates;
        
        public Tokenizer()
        {
            var rawTokenizerData = Config.RawTokenizerData.Buffer;
            
            Allocator = new();

            TokenizerHandle = TokenizerNativeMethods.AllocateTokenizer(
                rawTokenizerData.Ptr,
                rawTokenizerData.Length
            );
        }
        
        [SkipLocalsInit]
        public TokenizeOutput Tokenize(string input, bool addSpecialTokens = true)
        {
            Span<byte> allocation;

            var inputLength = input.Length;

            Unsafe.SkipInit(out NativeMemory<byte> nativeMemory);
            
            var allocateNative = inputLength > Config.ExpectedMaxInputLength;
            
            if (!allocateNative)
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
            
            var result = TokenizerNativeMethods.TokenizerEncode(
                TokenizerHandle, 
                u8String,
                addSpecialTokens,
                Truncate
            );
            
            if (allocateNative)
            {
                nativeMemory.Dispose();
            }
            
            return result;
        }
        
        public void TokenizeBatch(ReadOnlySpan<string> inputs, Span<TokenizeOutput> outputs, bool addSpecialTokens = true)
        {
            ref var first = ref MemoryMarshal.GetReference(outputs);
            
            fixed (TokenizeOutput* outputsPtr = &first)
            {
                TokenizeBatchInternal(
                    inputs, 
                    new(outputsPtr, (nuint) outputs.Length), 
                    skipLengthCheck: false,
                    addSpecialTokens: addSpecialTokens
                );
            }
        }
        
        public void TokenizeBatch(ReadOnlySpan<string> inputs, NativeBuffer<TokenizeOutput> outputs, bool addSpecialTokens = true)
        {
            TokenizeBatchInternal(
                inputs,
                outputs, 
                skipLengthCheck: false,
                addSpecialTokens: addSpecialTokens
            );
        }
        
        public NativeMemory<TokenizeOutput> TokenizeBatch(ReadOnlySpan<string> inputs, bool addSpecialTokens = true)
        {
            var outputs = new NativeMemory<TokenizeOutput>((nuint) inputs.Length);
            
            TokenizeBatchInternal(
                inputs, 
                outputs.Buffer,
                skipLengthCheck: true,
                addSpecialTokens: addSpecialTokens
            );
            
            return outputs;
        }
        
        const int MAX_STACK_ALLOC_NUM_INPUTS = 32;

        [InlineArray(MAX_STACK_ALLOC_NUM_INPUTS)]
        private struct FixedBuffer<T> where T: unmanaged
        {
            public T First;

            [UnscopedRef]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<T> AsSpan()
            {
                return this;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* AsPointerUnsafely()
            {
                var span = AsSpan();
                
                return (T*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
            }
        }
        
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TokenizeBatchInternal(
            ReadOnlySpan<string> inputs,
            NativeBuffer<TokenizeOutput> outputs,
            bool skipLengthCheck,
            bool addSpecialTokens)
        {
            var config = Config;
            
            var numInputs = (nuint) inputs.Length;
            
            if (!skipLengthCheck && numInputs != outputs.Length)
            {
                ThrowHelpers.TokenizeBatchInternalLengthCheckFailed();
            }

            Unsafe.SkipInit(out FixedBuffer<NativeMemory<byte>> nativeAllocationsFixedBuffer);
            Unsafe.SkipInit(out FixedBuffer<ReadOnlyNativeBuffer<byte>> u8StringsFixedBuffer);
            
            var nativeAllocations = new StackList<NativeMemory<byte>>(
                nativeAllocationsFixedBuffer.AsPointerUnsafely(),
                MAX_STACK_ALLOC_NUM_INPUTS
            );
            
            var u8Strings = new StackList<ReadOnlyNativeBuffer<byte>>(
                u8StringsFixedBuffer.AsPointerUnsafely(),
                MAX_STACK_ALLOC_NUM_INPUTS
            );
            
            var allocator = Allocator.GetHandle();
            
            foreach (var input in inputs)
            {
                NativeBuffer<byte> allocation;

                var inputLength = input.Length;
                    
                var allocateNative = 
                    inputLength > config.ExpectedMaxInputLength || 
                    (config.ExceedExpectedMaxBatchesBehavior == Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer && allocator.CurrentCount == 0);
                    
                if (!allocateNative)
                {
                    var arr = allocator.Allocate();
                    
                    // allocation = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(arr), arr.Length);
                    
                    allocation = new(arr, (nuint) arr.Length);
                }

                else
                {
                    var nativeMemory = new NativeMemory<byte>((nuint) Encoding.UTF8.GetMaxByteCount(inputLength));
                    
                    nativeAllocations.Add(nativeMemory);

                    allocation = nativeMemory.Buffer;
                }
                
                nuint bytesWritten;
                
                fixed (char* charPtr = input)
                {
                    bytesWritten = (nuint) Encoding.UTF8.GetBytes(
                        charPtr, 
                        charCount: input.Length, 
                        allocation.Ptr, 
                        (int) allocation.Length
                    );
                }
                
                u8Strings.Add(new(allocation.Ptr, bytesWritten));
            }
            
            var readonlyU8Strings = u8Strings.AsSlicedNativeBuffer().AsReadOnly();

            var tokenizerHandle = TokenizerHandle;
            
            var truncate = Truncate;
            
            TokenizerNativeMethods.TokenizerEncodeBatch(
                tokenizerPtr: tokenizerHandle,
                textNativeBuffers: readonlyU8Strings,
                outputNativeBuffer: outputs,
                addSpecialTokens,
                truncate
            );

            foreach (var nativeMemory in nativeAllocations.AsSpan())
            {
                nativeMemory.Dispose();
            }
            
            nativeAllocations.Dispose();
            u8Strings.Dispose();
            allocator.Dispose();
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