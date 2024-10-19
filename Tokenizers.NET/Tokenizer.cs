using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Tokenizers.NET.Collections;
using Tokenizers.NET.Enumerators;
using Tokenizers.NET.Helpers;
using Tokenizers.NET.Outputs;

namespace Tokenizers.NET
{
    public static unsafe class Tokenizer
    {
        public enum ExceedExpectedMaxBatchesBehavior
        {
            AllocateBuffer,
            // Discard, // TODO: Implement this
        }
        
        public interface IConfig
        {
            public static abstract ConfigBuilder ConfigBuilder { get; }
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
            
            internal BuiltConfig Build()
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
            
            public readonly Truncation? Truncation;

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
            
                var truncation = Truncation = tokenizerData.Truncation;
                
                Truncates = truncation != null;
            }
        }
        
        internal static class BuiltConfigCache<ConfigT> where ConfigT: struct, IConfig
        {
            public static readonly BuiltConfig BUILT_CONFIG = ConfigT.ConfigBuilder.Build();
        }
    }
    
    public readonly unsafe struct Tokenizer<ConfigT>: IDisposable
        where ConfigT: struct, Tokenizer.IConfig
    {
        private readonly struct TempFixedAllocator
        {
            private static Tokenizer.BuiltConfig Config
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Tokenizer.BuiltConfigCache<ConfigT>.BUILT_CONFIG;
            }
            
            private static readonly int 
                PER_BUFFER_SIZE = UTF8EncodingPirated.GetMaxByteCount(Config.ExpectedMaxInputLength.ToSignedUnchecked()),
                TOTAL_BUFFER_SIZE = PER_BUFFER_SIZE * Config.ExpectedMaxBatches.ToSignedUnchecked();
            
            // We need to keep a GC reference to it
            // Yes, technically we could malloc, but POH allocation does have its benefits,
            // such as the GC automatically cleaning it up when the entire tokenizer is out of scope.
            // It should also work better with existing .NET debugging tools.
            
            // While we could use Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(Buffers))
            // to obtain a pointer, GetArrayDataReference emit instructiont that does a null check.
            // We already know that Buffers will never be null...
            
            // ReSharper disable once NotAccessedField.Local
            private readonly byte[] Buffers;
            
            private readonly byte* BuffersPtr;

            private readonly int Count;

            // Modern cacheline size is either 64 or 128 bytes,
            // reducing cross-cacheline reads for SIMD instructions.
            // This should also satisfy the alignment for NativeBuffer<NativeBuffer<byte>>,
            // enabling us to reinterpret the memory in IDsToTokens() to avoid allocation.
            private const int ALIGNMENT = 128;

            static TempFixedAllocator()
            {
                Debug.Assert(ALIGNMENT % sizeof(NativeBuffer<NativeBuffer<byte>>) == 0);
            }
            
            public TempFixedAllocator()
            {
                var maxExpectedBatches = Config.ExpectedMaxBatches.ToSignedUnchecked();
                
                var buffers = Buffers = AllocationHelpers.AllocatePinnedUninitializedAligned<byte>(
                    TOTAL_BUFFER_SIZE,
                    ALIGNMENT,
                    out var buffersPtr
                );

                BuffersPtr = buffersPtr;

                Count = maxExpectedBatches;
                
                if (buffers.Length == 0)
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
                    CurrentCount = allocator.Count;
                }
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool TryAllocate(out NativeBuffer<byte> buffer)
                {
                    return Allocator.TryAllocate(ref this, out buffer);
                }
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool IsManagedAllocation(NativeBuffer<byte> buffer)
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
            private bool TryAllocate(ref Handle handle, out NativeBuffer<byte> buffer)
            {
                var count = handle.CurrentCount;
                
                var success = count != 0;

                Unsafe.SkipInit(out buffer);
                
                if (success)
                {
                    var readIndex = handle.CurrentCount = count - 1;
                    
                    // Don't clear underlying reference, this is to ensure that the buffer will not be collected by GC.
                    // When p/invoking, we pass the pointer to the buffer, so we don't want it to be collected.
                    // It is also more performant to avoid clearing...
                    buffer = new(BuffersPtr + (readIndex.ToUnsignedUnchecked() * (uint) PER_BUFFER_SIZE), (uint) PER_BUFFER_SIZE);
                }

                return success;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public NativeBuffer<byte> GetFullAllocationUnsafely()
            { 
                return new(BuffersPtr, (nuint) TOTAL_BUFFER_SIZE);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsManagedAllocation(NativeBuffer<byte> buffer)
            {
                // While it may be tempting to do buffer.Length == (nuint) PER_BUFFER_SIZE,
                // it is not resilient to sliced buffers ( Buffers are sliced based on the write count ),
                // as the write count could possibly be PER_BUFFER_SIZE ( However unlikely ).
                
                var startPtr = BuffersPtr;
                
                var currentPtr = buffer.Ptr;
                
                // I am pretty sure this can be optimized into a single comparison,
                // but let's play safe for now...
                return startPtr <= currentPtr && currentPtr < startPtr + TOTAL_BUFFER_SIZE;
            }
        }
        
        // ReSharper disable once NotAccessedField.Local
        // We need to keep a GC reference to it
        private readonly NativeBuffer<byte>[] U8StringBuffers;
        
        private readonly NativeBuffer<byte>* U8StringBuffersPtr;
        
        private readonly TempFixedAllocator Allocator;
        
        private readonly nint TokenizerHandle;

        public Tokenizer.BuiltConfig Config
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Tokenizer.BuiltConfigCache<ConfigT>.BUILT_CONFIG;
        }
        
        public bool Truncate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Config.Truncates;
        }
        
        public Tokenizer()
        {
            var config = Config;
            
            var expectedMaxBatches = config.ExpectedMaxBatches;
            
            var rawTokenizerData = config.RawTokenizerData.Buffer;
            
            var u8StringBuffers = U8StringBuffers = AllocationHelpers.AllocatePinnedUninitialized<NativeBuffer<byte>>(
                expectedMaxBatches
            );
            
            U8StringBuffersPtr = u8StringBuffers.PinnedArrayToPointer();
            
            Allocator = new();

            TokenizerHandle = TokenizerNativeMethods.AllocateTokenizer(rawTokenizerData);
        }
        
        public TokenizeOutput Tokenize(string input, bool addSpecialTokens = true)
        {
            return TokenizeInternal(input, addSpecialTokens);
        }
        
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TokenizeOutput TokenizeInternal(string input, bool addSpecialTokens)
        {
            var inputLength = input.Length;

            var allocateNative = inputLength > (Config.ExpectedMaxInputLength * Config.ExpectedMaxBatches);

            NativeBuffer<byte> allocation;
            
            if (!allocateNative)
            {
                allocation = Allocator.GetFullAllocationUnsafely();
                
                #if DEBUG
                Debug.Assert(allocation.Length >= (nuint) Encoding.UTF8.GetMaxByteCount(inputLength));
                #endif
            }

            else
            {
                allocation = new NativeMemory<byte>((nuint) UTF8EncodingPirated.GetMaxByteCount(inputLength)).Buffer;
            }

            var bytesWritten = Encoding.UTF8.GetBytes(input, allocation.AsSpan());
            
            var u8String = new NativeBuffer<byte>(allocation.Ptr, (nuint) bytesWritten);
            
            var result = TokenizerNativeMethods.TokenizerEncode(
                TokenizerHandle, 
                u8String,
                addSpecialTokens,
                Truncate
            );
            
            if (allocateNative)
            {
                NativeMemory<byte>.WrapBuffer(allocation).Dispose();
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
        
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TokenizeBatchInternal(
            ReadOnlySpan<string> inputs,
            NativeBuffer<TokenizeOutput> outputs,
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
                U8StringBuffersPtr :
                new NativeMemory<NativeBuffer<byte>>(numInputs).Buffer.Ptr;
            
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

                    allocation = nativeMemory.Buffer;
                }
                
                var bytesWritten = Encoding.UTF8.GetBytes(input, allocation.AsSpan());
                
                *currentU8String = new(allocation.Ptr, (nuint) bytesWritten);
                
                currentU8String++;
            }
            
            var readonlyU8Strings = new NativeBuffer<NativeBuffer<byte>>(
                u8StringsPtr,
                numInputs
            );

            var tokenizerHandle = TokenizerHandle;
            
            var truncate = Truncate;
            
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
            
            NativeMemory<NativeBuffer<byte>>.FreeWithPtrUnsafely(readonlyU8Strings.Ptr);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput Decode(ReadOnlySpan<uint> ids, bool skipSpecialTokens)
        {
            ref var first = ref MemoryMarshal.GetReference(ids);
            
            fixed(uint* ptr = &first)
            {
                return Decode((NativeBuffer<uint>) new(ptr, (nuint) ids.Length), skipSpecialTokens);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecodeOutput Decode(NativeBuffer<uint> ids, bool skipSpecialTokens)
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
        public FreeHandle IDsToTokens(NativeBuffer<uint> ids, Span<NativeBuffer<byte>> u8Strings)
        {
            fixed (NativeBuffer<byte>* ptr = &MemoryMarshal.GetReference(u8Strings))
            {
                var u8StringsBuffer = new NativeBuffer<NativeBuffer<byte>>(ptr, (nuint) u8Strings.Length);
                
                return IDsToTokens(ids, u8StringsBuffer);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FreeHandle IDsToTokens(
            NativeBuffer<uint> ids,
            NativeBuffer<NativeBuffer<byte>> tokens,
            bool performSizeCheck = true)
        {
            if (performSizeCheck && tokens.Length < ids.Length)
            {
                ThrowHelpers.IDsToTokens_LengthCheckFailed();
            }
            
            var tokenizerHandle = TokenizerHandle;

            return new(TokenizerNativeMethods.IDsToTokens(tokenizerHandle, ids, tokens));
        }
        
        public string[] IDsToTokens(NativeBuffer<uint> ids)
        {
            var tokens = new string[ids.Length];
            
            IDsToTokens(ids, tokens, performSizeCheck: false);
            
            return tokens;
        }
        
        public void IDsToTokens(NativeBuffer<uint> ids, Span<string> tokens, bool performSizeCheck = true)
        {
            var inputLength = ids.Length;
            
            if (performSizeCheck && (nuint) tokens.Length < inputLength)
            {
                ThrowHelpers.IDsToTokens_LengthCheckFailed();
            }

            var allocationSizeInBytes = (int) inputLength * sizeof(NativeBuffer<NativeBuffer<byte>>);

            var allocateNative = allocationSizeInBytes > (Config.ExpectedMaxInputLength * Config.ExpectedMaxBatches);
            
            NativeBuffer<NativeBuffer<byte>> allocation;
            
            if (!allocateNative)
            {
                var ptr = Allocator.GetFullAllocationUnsafely().Ptr;
                
                allocation = new((NativeBuffer<byte>*) ptr, inputLength);
            }

            else
            {
                allocation = new NativeMemory<NativeBuffer<byte>>(inputLength).Buffer;
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
                NativeMemory<NativeBuffer<byte>>.FreeWithPtrUnsafely(allocation.Ptr);
            }
        }
        
        public void Dispose()
        {
            TokenizerNativeMethods.FreeTokenizer(TokenizerHandle);

            Unsafe.AsRef(in this) = default;
        }
    }
}