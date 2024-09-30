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
    
    public readonly unsafe struct Tokenizer<ConfigT>: IDisposable
        where ConfigT: struct, Tokenizer.IConfig
    {
        private readonly struct TempFixedAllocator
        {
            public static readonly int 
                PER_BUFFER_SIZE = Encoding.UTF8.GetMaxByteCount(ConfigT.BuiltConfig.ExpectedMaxInputLength.ToSignedUnchecked()),
                TOTAL_BUFFER_SIZE = PER_BUFFER_SIZE * ConfigT.BuiltConfig.ExpectedMaxBatches.ToSignedUnchecked();
            
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

            public TempFixedAllocator()
            {
                var config = ConfigT.BuiltConfig;

                var maxExpectedBatches = config.ExpectedMaxBatches.ToSignedUnchecked();
                
                var buffers = Buffers = AllocationHelpers.AllocatePinnedUninitialized<byte>(
                    TOTAL_BUFFER_SIZE
                );
                
                BuffersPtr = (byte*) Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(buffers));

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
        }
        
        private readonly TempFixedAllocator Allocator;
        
        private readonly nint TokenizerHandle;

        public Tokenizer.BuiltConfig Config
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ConfigT.BuiltConfig;
        }
        
        public bool Truncate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Config.Truncates;
        }
        
        public Tokenizer()
        {
            var rawTokenizerData = Config.RawTokenizerData.Buffer.AsReadOnly();
            
            Allocator = new();

            TokenizerHandle = TokenizerNativeMethods.AllocateTokenizer(rawTokenizerData);
        }
        
        [SkipLocalsInit]
        public TokenizeOutput Tokenize(string input, bool addSpecialTokens = true)
        {
            Span<byte> allocation;

            var inputLength = input.Length;

            Unsafe.SkipInit(out NativeMemory<byte> nativeMemory);

            var allocateNative = inputLength > TempFixedAllocator.TOTAL_BUFFER_SIZE;
            
            if (!allocateNative)
            {
                allocation = Allocator.GetFullAllocationUnsafely().AsSpan();
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
            private T First;

            [UnscopedRef]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<T> AsSpan()
            {
                return this;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* AsPointerUnsafely()
            {
                return (T*) Unsafe.AsPointer(ref First);
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
            
            var inputsEnumerator = new UnsafeReadOnlySpanEnumerator<string>(inputs);
            
            foreach (var input in inputsEnumerator)
            {
                Span<byte> allocation;

                var inputLength = input.Length;
                
                if (inputLength <= Config.ExpectedMaxInputLength && allocator.TryAllocate(out var buffer))
                {
                    allocation = buffer.AsSpan();
                }

                else
                {
                    var nativeMemory = new NativeMemory<byte>((nuint) Encoding.UTF8.GetMaxByteCount(inputLength));
                    
                    nativeAllocations.Add(nativeMemory);

                    allocation = nativeMemory.Buffer.AsSpan();
                }
                
                var bytesWritten = Encoding.UTF8.GetBytes(input, allocation);
                
                u8Strings.Add(new(
                    (byte*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(allocation)),
                    (nuint) bytesWritten)
                );
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

            foreach (var nativeMemory in nativeAllocations)
            {
                nativeMemory.Dispose();
            }
            
            nativeAllocations.Dispose();
            u8Strings.Dispose();
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
            TokenizerNativeMethods.FreeTokenizer(TokenizerHandle);

            Unsafe.AsRef(in this) = default;
        }
    }
}