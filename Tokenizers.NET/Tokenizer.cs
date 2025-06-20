using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using NativeMemory;
using NoParamlessCtor.Shared.Attributes;
using Tokenizers.NET.Enumerators;
using Tokenizers.NET.Helpers;
using Tokenizers.NET.Outputs;

namespace Tokenizers.NET
{
    [NoParamlessCtor]
    public readonly unsafe partial struct Tokenizer: IDisposable
    {
        // Modern cacheline size is either 64 or 128 bytes,
        // reducing cross-cacheline reads for SIMD instructions.
        // This should also satisfy the alignment for MemoryWindow<MemoryWindow<byte>>,
        // enabling us to reinterpret the memory in IDsToTokens() to avoid allocation.
        private const int ALIGNMENT = 128;

        [NoParamlessCtor]
        private readonly partial struct TempFixedAllocator
        {
            // We need to keep a GC reference to it
            // Yes, technically we could malloc, but POH allocation does have its benefits,
            // such as the GC automatically cleaning it up when the entire tokenizer is out of scope.
            // It should also work better with existing .NET debugging tools.
            
            // While we could use Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(Buffers))
            // to obtain a pointer, GetArrayDataReference emit instruction that does a null check.
            // We already know that Buffers will never be null...

            private readonly PinnedArrayMemory<byte> BuffersMemory;

            private readonly int AllocatedBufferCount;

            private readonly nuint PerBufferSize;

            static TempFixedAllocator()
            {
                Debug.Assert(ALIGNMENT % sizeof(MemoryWindow<MemoryWindow<byte>>) == 0);
            }
            
            public TempFixedAllocator(TokenizerConfig config)
            {
                var expectedMaxBatches = config.ExpectedMaxBatches.ToSignedUnchecked();

                var perBufferSize = UTF8EncodingPirated.GetMaxByteCount(
                    config.ExpectedMaxInputLength.ToSignedUnchecked()
                );

                PerBufferSize = perBufferSize.ToNuintUnchecked();

                var totalBufferSize = perBufferSize * expectedMaxBatches;

                BuffersMemory = new(
                    length: totalBufferSize,
                    zeroed: true,
                    alignment: ALIGNMENT
                );

                AllocatedBufferCount = expectedMaxBatches;
                
                if (BuffersMemory.AlignedLength == 0)
                {
                    throw new InvalidOperationException("Buffers length cannot be zero.");
                }
            }

            public ref struct Handle
            {
                private readonly ref readonly TempFixedAllocator Allocator;

                internal int CurrentCount;
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal Handle(ref readonly TempFixedAllocator allocator)
                {
                    Allocator = ref allocator;
                    CurrentCount = allocator.AllocatedBufferCount;
                }
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool TryAllocate(out MemoryWindow<byte> buffer)
                {
                    return Allocator.TryAllocate(ref this, out buffer);
                }
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool IsManagedAllocation(MemoryWindow<byte> buffer)
                {
                    return Allocator.IsManagedAllocation(buffer);
                }
            }
            
            [UnscopedRef]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Handle GetHandle()
            {
                return new(in this);
            }
            
            [SkipLocalsInit]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAllocate(ref Handle handle, out MemoryWindow<byte> buffer)
            {
                var count = handle.CurrentCount;
                
                var success = count != 0;

                Unsafe.SkipInit(out buffer);
                
                if (success)
                {
                    var readIndex = handle.CurrentCount = count - 1;

                    var perBufferSize = PerBufferSize;

                    var buffersPtr = BuffersMemory.Window.Ptr;

                    // Don't clear underlying reference, this is to ensure that the buffer will not be collected by GC.
                    // When p/invoking, we pass the pointer to the buffer, so we don't want it to be collected.
                    // It is also more performant to avoid clearing...
                    buffer = new(buffersPtr + (readIndex.ToNuintUnchecked() * perBufferSize), perBufferSize);
                }

                return success;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public MemoryWindow<byte> GetFullAllocationUnsafely()
            {
                return BuffersMemory.Window;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsManagedAllocation(MemoryWindow<byte> buffer)
            {
                // While it may be tempting to do buffer.Length == (nuint) PER_BUFFER_SIZE,
                // it is not resilient to sliced buffers ( Buffers are sliced based on the write count ),
                // as the write count could possibly be PER_BUFFER_SIZE ( However unlikely ).

                ref readonly var buffersWindow = ref BuffersMemory.Window;

                var buffersPtr = buffersWindow.Ptr;
                
                var currentPtr = buffer.Ptr;
                
                // I am pretty sure this can be optimized into a single comparison,
                // but let's play safe for now...
                return buffersPtr <= currentPtr && currentPtr < buffersPtr + buffersWindow.Length;
            }
        }

        public readonly TokenizerConfig Config;

        private readonly PinnedArrayMemory<MemoryWindow<byte>> U8StringBuffers;
        
        private readonly TempFixedAllocator Allocator;
        
        private readonly nint TokenizerHandle;
        
        public bool IsTruncating
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Config.Truncates;
        }

        internal Tokenizer(TokenizerBuilder builder)
        {
            var config = Config = builder.BuildConfig();
            
            var expectedMaxBatches = config.ExpectedMaxBatches;

            using var rawTokenizerData = config.RawTokenizerData;
            
            U8StringBuffers = new PinnedArrayMemory<MemoryWindow<byte>>(
                (int) expectedMaxBatches,
                zeroed: false,
                alignment: ALIGNMENT
            );
            
            Allocator = new(config);

            TokenizerHandle = TokenizerNativeMethods.AllocateTokenizer(rawTokenizerData.Window);
        }
        
        public TokenizeOutput Tokenize(string input, bool addSpecialTokens = true)
        {
            return TokenizeInternal(input, addSpecialTokens);
        }

        public TokenizeOutput Tokenize(ReadOnlySpan<char> input, bool addSpecialTokens = true)
        {
            return TokenizeInternal(input, addSpecialTokens);
        }
        
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TokenizeOutput TokenizeInternal(ReadOnlySpan<char> input, bool addSpecialTokens)
        {
            var inputLength = input.Length;

            var allocateNative = inputLength > (Config.ExpectedMaxInputLength * Config.ExpectedMaxBatches);

            MemoryWindow<byte> allocation;
            
            if (!allocateNative)
            {
                allocation = Allocator.GetFullAllocationUnsafely();
                
                #if DEBUG
                Debug.Assert(allocation.Length >= (nuint) Encoding.UTF8.GetMaxByteCount(inputLength));
                #endif
            }

            else
            {
                allocation = new NativeMemory<byte>((nuint) UTF8EncodingPirated.GetMaxByteCount(inputLength)).Window;
            }

            var bytesWritten = Encoding.UTF8.GetBytes(input, allocation.AsSpan());
            
            var u8String = new MemoryWindow<byte>(allocation.Ptr, (nuint) bytesWritten);
            
            var result = TokenizerNativeMethods.TokenizerEncode(
                TokenizerHandle, 
                u8String,
                addSpecialTokens,
                IsTruncating
            );
            
            if (allocateNative)
            {
                NativeMemory<byte>
                    .WrapBufferUnsafely(allocation)
                    .Dispose();
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
        
        public void TokenizeBatch(ReadOnlySpan<string> inputs, MemoryWindow<TokenizeOutput> outputs, bool addSpecialTokens = true)
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
                outputs.Window,
                skipLengthCheck: true,
                addSpecialTokens: addSpecialTokens
            );
            
            return outputs;
        }
        
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TokenizeBatchInternal(
            ReadOnlySpan<string> inputs,
            MemoryWindow<TokenizeOutput> outputs,
            bool skipLengthCheck,
            bool addSpecialTokens)
        {
            var numInputs = (nuint) inputs.Length;
            
            if (!skipLengthCheck && numInputs != outputs.Length)
            {
                ThrowHelpers.TokenizeBatchInternal_LengthCheckFailed();
            }

            var exceedsExpectedMaxBatches = numInputs > Config.ExpectedMaxBatches;
            
            var u8StringsPtr = !exceedsExpectedMaxBatches ?
                U8StringBuffers.Window.Ptr :
                new NativeMemory<MemoryWindow<byte>>(numInputs).Window.Ptr;
            
            var allocator = Allocator.GetHandle();
            
            var inputsEnumerator = new UnsafeReadOnlySpanEnumerator<string>(inputs);
            
            var currentU8String = u8StringsPtr;
            
            foreach (var input in inputsEnumerator)
            {
                var inputLength = input.Length;
                
                if (inputLength <= Config.ExpectedMaxInputLength && 
                    allocator.TryAllocate(out var allocation))
                {
                    // Nothing here
                }

                else
                {
                    var nativeMemory = new NativeMemory<byte>((nuint) UTF8EncodingPirated.GetMaxByteCount(inputLength));

                    allocation = nativeMemory.Window;
                }
                
                var bytesWritten = Encoding.UTF8.GetBytes(input, allocation.AsSpan());
                
                *currentU8String = new(allocation.Ptr, (nuint) bytesWritten);
                
                currentU8String++;
            }
            
            var readonlyU8Strings = new MemoryWindow<MemoryWindow<byte>>(
                u8StringsPtr,
                numInputs
            );

            var tokenizerHandle = TokenizerHandle;
            
            var truncate = IsTruncating;
            
            TokenizerNativeMethods.TokenizerEncodeBatch(
                tokenizerPtr: tokenizerHandle,
                textNativeBuffers: readonlyU8Strings,
                outputNativeBuffer: outputs,
                addSpecialTokens,
                truncate
            );

            foreach (var buffer in readonlyU8Strings)
            {
                if (!allocator.IsManagedAllocation(buffer))
                {
                    NativeMemory<byte>.FreeWithPtrUnsafely(buffer.Ptr);
                }
            }

            if (!exceedsExpectedMaxBatches)
            {
                return;
            }
            
            NativeMemory<MemoryWindow<byte>>.FreeWithPtrUnsafely(readonlyU8Strings.Ptr);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput Decode(ReadOnlySpan<uint> ids, bool skipSpecialTokens)
        {
            ref var first = ref MemoryMarshal.GetReference(ids);
            
            fixed(uint* ptr = &first)
            {
                return Decode((MemoryWindow<uint>) new(ptr, (nuint) ids.Length), skipSpecialTokens);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput Decode(MemoryWindow<uint> ids, bool skipSpecialTokens)
        {
            var tokenizerHandle = TokenizerHandle;
            
            return TokenizerNativeMethods.TokenizerDecode(tokenizerHandle, ids, skipSpecialTokens);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput DecodeMutating(Span<ulong> ids, bool skipSpecialTokens)
        {
            fixed (ulong* ptr = &MemoryMarshal.GetReference(ids))
            {
                var idsBuffer = new MemoryWindow<ulong>(ptr, (nuint) ids.Length);
                
                return DecodeMutating(idsBuffer, skipSpecialTokens);
            }
        }
        
        public DecodeOutput DecodeMutating(MemoryWindow<ulong> ids, bool skipSpecialTokens)
        {
            var tokenizerHandle = TokenizerHandle;

            var mutated = ids.NarrowMutating();
            
            // The length should still be the same, even though the actual underlying length is double
            mutated = new(mutated.Ptr, ids.Length);
            
            return TokenizerNativeMethods.TokenizerDecode(
                tokenizerHandle, 
                mutated,
                skipSpecialTokens
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FreeHandle IDsToTokens(MemoryWindow<uint> ids, Span<MemoryWindow<byte>> u8Strings)
        {
            fixed (MemoryWindow<byte>* ptr = &MemoryMarshal.GetReference(u8Strings))
            {
                var u8StringsBuffer = new MemoryWindow<MemoryWindow<byte>>(ptr, (nuint) u8Strings.Length);
                
                return IDsToTokens(ids, u8StringsBuffer);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FreeHandle IDsToTokens(
            MemoryWindow<uint> ids,
            MemoryWindow<MemoryWindow<byte>> tokens,
            bool performSizeCheck = true)
        {
            if (performSizeCheck && tokens.Length < ids.Length)
            {
                ThrowHelpers.IDsToTokens_LengthCheckFailed();
            }
            
            var tokenizerHandle = TokenizerHandle;

            return new(TokenizerNativeMethods.IDsToTokens(tokenizerHandle, ids, tokens));
        }
        
        public string[] IDsToTokens(MemoryWindow<uint> ids)
        {
            var tokens = new string[ids.Length];
            
            IDsToTokens(ids, tokens, performSizeCheck: false);
            
            return tokens;
        }
        
        public void IDsToTokens(MemoryWindow<uint> ids, Span<string> tokens, bool performSizeCheck = true)
        {
            var inputLength = ids.Length;
            
            if (performSizeCheck && (nuint) tokens.Length < inputLength)
            {
                ThrowHelpers.IDsToTokens_LengthCheckFailed();
            }

            var allocationSizeInBytes = (int) inputLength * sizeof(MemoryWindow<MemoryWindow<byte>>);

            var allocateNative = allocationSizeInBytes > (Config.ExpectedMaxInputLength * Config.ExpectedMaxBatches);
            
            MemoryWindow<MemoryWindow<byte>> allocation;
            
            if (!allocateNative)
            {
                var ptr = Allocator.GetFullAllocationUnsafely().Ptr;
                
                allocation = new((MemoryWindow<byte>*) ptr, inputLength);
            }

            else
            {
                allocation = new NativeMemory<MemoryWindow<byte>>(inputLength).Window;
            }
            
            using var freeHandle = IDsToTokens(ids, allocation, performSizeCheck: false);

            ref var currentToken = ref MemoryMarshal.GetReference(tokens);
            
            foreach (var buffer in allocation)
            {
                // In theory, we could intern the tokenizer's vocab and greatly reduce string allocs,
                // but it is what it is for now...
                currentToken = Encoding.UTF8.GetString(buffer.Ptr, (int) buffer.Length);
                
                currentToken = ref Unsafe.Add(ref currentToken, 1);
            }
            
            if (allocateNative)
            {
                NativeMemory<MemoryWindow<byte>>.FreeWithPtrUnsafely(allocation.Ptr);
            }
        }
        
        public void Dispose()
        {
            TokenizerNativeMethods.FreeTokenizer(TokenizerHandle);

            Unsafe.AsRef(in this) = default;
        }
    }
}