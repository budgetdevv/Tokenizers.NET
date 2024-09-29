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
            TokenizeBatchInternal(
                inputs, 
                outputs, 
                outputsPrePinned: false,
                skipLengthCheck: false,
                addSpecialTokens: addSpecialTokens
            );
        }
        
        public void TokenizeBatch(ReadOnlySpan<string> inputs, NativeMemory<TokenizeOutput> outputs, bool addSpecialTokens = true)
        {
            TokenizeBatchInternal(
                inputs, 
                outputs.Buffer.AsSpan(), 
                outputsPrePinned: true,
                skipLengthCheck: false,
                addSpecialTokens: addSpecialTokens
            );
        }
        
        public NativeMemory<TokenizeOutput> TokenizeBatch(ReadOnlySpan<string> inputs, bool addSpecialTokens = true)
        {
            var outputs = new NativeMemory<TokenizeOutput>((nuint) inputs.Length);
            
            TokenizeBatchInternal(
                inputs, 
                outputs.Buffer.AsSpan(), 
                outputsPrePinned: true,
                skipLengthCheck: true,
                addSpecialTokens: addSpecialTokens
            );
            
            return outputs;
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TokenizeBatchInternal(
            ReadOnlySpan<string> inputs,
            Span<TokenizeOutput> outputs,
            bool outputsPrePinned,
            bool skipLengthCheck,
            bool addSpecialTokens)
        {
            var config = Config;
            
            var numInputs = inputs.Length;
            
            if (!skipLengthCheck && numInputs != outputs.Length)
            {
                ThrowHelpers.TokenizeBatchInternalLengthCheckFailed();
            }
            
            // Workaround for issue described in https://github.com/dotnet/runtime/pull/108356,
            // which prevents this method from being inlined, even with AggressiveInlining
            
            // See TokenizeOutput.Dispose() for more information
            
            // How PESSIMIZED_NATIVE_MEMORY_SIZE is calculated:
            // https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA0AXEUCuA7AHwAEAmARgFgAoIgBgAIiyA6AJXwwEsBbGZgYQjcADpwA2MKAGVJAN05gYAZwDc1Ooxbs8XXswCSOyRGEyo8xaurV8SgIYAzGNQDe1eh80BOABRLOAF4wEA4+AHJ2XLIwALIw3NAAngA8wIkYMAB8AJTZalSe3n6BwaERUTAAQjgOTlCp6Vm5+QC+1lQA2lIYuGAYADJ2iRA4GD6Dw6MA0px4ACbMMgCOODA6nHZi2QC66gDM9LB2cxB4Yon0to4w9Eo9OH305ZzRcQlQKQAqmfQg9PoAEU4SmEEHswAkaH+AFEVpE7BCYMlnq94klkt9Mu5PAB3AAWkhun1+lzw3DseDsAHMYHNXNiPEQDkcTmcLiiqjU6hiftVapJ8gzGAcOW8kj5skK3AVCp4MHioBAcfQ8DBlWEIBgpDhhKCoBk5tCEIphFxThL8oU2lQhUynpEXrE0R8fHgcLMMPQJHgqfLJTKPNLZZ5ZHYoPRTeGALz0HyfABU2VFzsSzAAgmIxBAwD5vb68VDXe6dNl/EEQnHmu1gx4+XV6DHVTifJGoXm/ZbPNahR04vKIHN9CIxD4+3iB0PhGIAPKmzinJTpqlU2BKfzRQxiWazKk7W0HJhIe0VMUfHn0ADqUDswjrknCDuid/q33owC5kn9hSDNaIAHZ6AAVTwewnGYSpOAwfg7DuZFH05fkX0yKFk3eL5MkyHx30QvIhW7AN6F7GB+0HYdR2I8dSKnWczRApcV2UdcYE3bcfT3Ai7WACAIDEehYRwTYlAfE8U3PTUCSgL9PB/YNQ3DcTJGfBt6AUqBwI/KBO1ksM3w05Tn0FAjCn/FT5UUjTmAABR6BsY2wuorJsgAyJzTIk59mH6NZ81s3TEM87z5S0+h8OMg4IGiKAoE4OYbi4ni+LhMQhIgYAACsYD6AB+FT0qkwMhWMgDUrS+hgWPR1T3QtzJHoFzEoE5KfFU3CCNCzwiJIycRzHCdhxo+c6LTZdVyYli8B3diwpUyLotisqdHoABxYiAAkYLxQRYolKVCs8EyfA9bIPOszS8OrfaDzII94t4kxJEiaBbOEyrRNfCQHAwFD4Kq89oqpPEMHy+gZNlEyPowZh+MEnx/sB1qrQuxkrpu7i7uEB6MCegBCKMXtRNDzwh76RMJ184aB3ajP2gDsYhqGkqEimEa7JHhUYFB6CBEEwRgHaCNBwpUKSZgADFYD5k6ehZjxrXwrp7j6CYRjGZXplmBZllWdZNnYu0WVOc5SVAm47l6T0OWfHk43jCMeihN0PS9QK8Wyeh8UJehiT+fByUpGk6SoUH9ZgY5DYuBN6FO5TI2CkOw7ZFVi09LyfXlZT2zxVogA
            
            const int
                MAX_STACK_ALLOC_NUM_INPUTS = 2,
                PESSIMIZED_NATIVE_MEMORY_SIZE = 16,
                STACK_ALLOC_SIZE_PER_INSTANCE = MAX_STACK_ALLOC_NUM_INPUTS * PESSIMIZED_NATIVE_MEMORY_SIZE;
            
            // STACK_ALLOC_SIZE_PER_INSTANCE must be <= 32, unfortunately...
            // If not, inlining is blocked for some reason
            var nativeAllocationsPtr = stackalloc byte[STACK_ALLOC_SIZE_PER_INSTANCE];
            
            var u8StringsPtr = stackalloc byte[STACK_ALLOC_SIZE_PER_INSTANCE];
            
            #if DEBUG
            Debug.Assert(sizeof(NativeMemory<byte>) == sizeof(ReadOnlyNativeBuffer<byte>));
            Debug.Assert(sizeof(NativeMemory<byte>) <= PESSIMIZED_NATIVE_MEMORY_SIZE);
            
            // Ensure address are naturally aligned
            Debug.Assert(((nint) nativeAllocationsPtr) % sizeof(NativeMemory<byte>) == 0);
            Debug.Assert(((nint) u8StringsPtr) % sizeof(NativeMemory<byte>) == 0);
            #endif
            
            var nativeAllocations = new StackList<NativeMemory<byte>>(
                (NativeMemory<byte>*) nativeAllocationsPtr, MAX_STACK_ALLOC_NUM_INPUTS
            );
            
            var u8Strings = new StackList<ReadOnlyNativeBuffer<byte>>(
                (ReadOnlyNativeBuffer<byte>*) u8StringsPtr, MAX_STACK_ALLOC_NUM_INPUTS
            );
            
            var allocator = Allocator.GetHandle();
            
            foreach (var input in inputs)
            {
                Span<byte> allocation;

                var inputLength = input.Length;
                    
                var allocateNative = 
                    inputLength > config.ExpectedMaxInputLength || 
                    (config.ExceedExpectedMaxBatchesBehavior == Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer && allocator.CurrentCount == 0);
                    
                if (!allocateNative)
                {
                    allocation = allocator.Allocate();
                }

                else
                {
                    var nativeMemory = new NativeMemory<byte>((nuint) Encoding.UTF8.GetMaxByteCount(inputLength));
                    
                    nativeAllocations.Add(nativeMemory);
                        
                    allocation = nativeMemory.Buffer.AsSpan();
                }

                var bytesWritten = Encoding.UTF8.GetBytes(input, allocation);
                
                u8Strings.Add(new(ref MemoryMarshal.GetReference(allocation), (nuint) bytesWritten));
            }
            
            var readonlyU8Strings = u8Strings.AsSlicedNativeBuffer().AsReadOnly();

            var tokenizerHandle = TokenizerHandle;
            
            var outputLength = (nuint) outputs.Length;
            
            ref var outputStart = ref MemoryMarshal.GetReference(outputs);

            var truncate = Truncate;
            
            if (outputsPrePinned)
            {
                TokenizerNativeMethods.TokenizerEncodeBatch(
                    tokenizerPtr: tokenizerHandle,
                    textNativeBuffers: readonlyU8Strings,
                    outputNativeBuffer: new(ref outputStart, outputLength),
                    addSpecialTokens,
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
                        outputNativeBuffer: new(outputsPtr, outputLength),
                        addSpecialTokens,
                        truncate
                    );
                }
            }

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