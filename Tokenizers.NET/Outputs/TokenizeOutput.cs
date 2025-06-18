using System.Runtime.InteropServices;
using NativeMemory;
using Tokenizers.NET.Helpers;

namespace Tokenizers.NET.Outputs
{
    public interface ITokenizeOutput
    {
        public MemoryWindow<uint> IDs { get; }
        
        public MemoryWindow<uint> AttentionMask { get; }
        
        public MemoryWindow<uint> SpecialTokensMask { get; }
        
        public MemoryWindow<uint> TokenTypeIDs { get; } 
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TokenizeOutputOverflowedToken: ITokenizeOutput
    {
        public readonly MemoryWindow<uint> IDs;

        public readonly MemoryWindow<uint> AttentionMask;

        public readonly MemoryWindow<uint> SpecialTokensMask;
        
        public readonly MemoryWindow<uint> TokenTypeIDs;
        
        MemoryWindow<uint> ITokenizeOutput.IDs => IDs;
        
        MemoryWindow<uint> ITokenizeOutput.AttentionMask => AttentionMask;
        
        MemoryWindow<uint> ITokenizeOutput.SpecialTokensMask => SpecialTokensMask;
        
        MemoryWindow<uint> ITokenizeOutput.TokenTypeIDs => TokenTypeIDs;
    }
    
    [StructLayout(LayoutKind.Sequential)] // Data structures
    public readonly unsafe partial struct TokenizeOutput: ITokenizeOutput, IDisposable
    {
        public readonly MemoryWindow<uint> IDs;

        public readonly MemoryWindow<uint> AttentionMask;

        public readonly MemoryWindow<uint> SpecialTokensMask;

        public readonly MemoryWindow<uint> TokenTypeIDs;
        
        public readonly MemoryWindow<TokenizeOutputOverflowedToken> OverflowingTokens;

        public readonly nint OriginalOutputFreeHandle;
        
        private readonly nint OverflowingTokensFreeHandle;
        
        MemoryWindow<uint> ITokenizeOutput.IDs => IDs;
        
        MemoryWindow<uint> ITokenizeOutput.AttentionMask => AttentionMask;
        
        MemoryWindow<uint> ITokenizeOutput.SpecialTokensMask => SpecialTokensMask;
        
        MemoryWindow<uint> ITokenizeOutput.TokenTypeIDs => TokenTypeIDs;
        
        private interface IGatherFieldAccessor
        {
            public static abstract MemoryWindow<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput;
        }

        private struct AccessIDs: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MemoryWindow<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.IDs;
            }
        }
        
        private struct AccessAttentionMask: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MemoryWindow<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.AttentionMask;
            }
        }
        
        private struct AccessSpecialTokensMask: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MemoryWindow<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.SpecialTokensMask;
            }
        }
        
        private struct AccessTokenTypeIDs: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MemoryWindow<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.TokenTypeIDs;
            }
        }
    }
    
    // Gather APIs
    public readonly unsafe partial struct TokenizeOutput
    {
        public void GatherIDsInclusiveOfOverflowing(
            MemoryWindow<uint> idsBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherIDsInclusiveOfOverflowing()
        {
            var idsBuffer = new NativeMemory<uint>(IDs.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return idsBuffer;
        }
        
        public void GatherAttentionMaskInclusiveOfOverflowing(
            MemoryWindow<uint> attentionMaskBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer, 
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherAttentionMaskInclusiveOfOverflowing()
        {
            var attentionMaskBuffer = new NativeMemory<uint>(AttentionMask.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return attentionMaskBuffer;
        }
        
        public void GatherSpecialTokensMaskInclusiveOfOverflowing(
            MemoryWindow<uint> specialTokensMaskBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherSpecialTokensMaskInclusiveOfOverflowing()
        {
            var specialTokensMaskBuffer = new NativeMemory<uint>(SpecialTokensMask.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return specialTokensMaskBuffer;
        }
        
        public void GatherTokenTypeIDsInclusiveOfOverflowing(
            MemoryWindow<uint> tokenTypeIDsBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessTokenTypeIDs>(
                tokenTypeIDsBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherTokenTypeIDsInclusiveOfOverflowing()
        {
            var tokenTypeIDsBuffer = new NativeMemory<uint>(TokenTypeIDs.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessTokenTypeIDs>(
                tokenTypeIDsBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return tokenTypeIDsBuffer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GatherIDsInclusiveOfOverflowingCore<FieldAccessorT>(
            MemoryWindow<uint> idsBuffer, 
            bool performRangeCheck,
            out nuint totalLength)
            where FieldAccessorT: struct, IGatherFieldAccessor
        {
            var sourceBuffer = FieldAccessorT.AccessField(this);

            var overflowingTokens = OverflowingTokens;
            
            var segmentLength = (int) sourceBuffer.Length;

            var numSegments = overflowingTokens.Length + 1;
            
            totalLength = numSegments * (nuint) segmentLength;
            
            if (performRangeCheck)
            {
                if (idsBuffer.Length < totalLength)
                {
                    throw new ArgumentException("The provided buffer is too small.");
                }
            }

            var currentDestPtr = idsBuffer.Ptr;
            
            var currentSourceSpan = sourceBuffer.AsReadOnlySpan();

            var enumerator = overflowingTokens.AsReadOnlySpan().GetEnumerator();

            while (true)
            {
                currentSourceSpan.CopyTo(new(currentDestPtr, segmentLength));
                
                currentDestPtr += segmentLength;
                
                if (enumerator.MoveNext())
                {
                    currentSourceSpan = FieldAccessorT.AccessField(enumerator.Current).AsReadOnlySpan();
                    continue;
                }
                
                return;
            }
        }
    }

    // Gather and Widen APIs
    public readonly unsafe partial struct TokenizeOutput
    {
        public void GatherAndWidenIDsInclusiveOfOverflowing(
            MemoryWindow<ulong> idsBuffer, 
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }

        public NativeMemory<ulong> GatherAndWidenIDsInclusiveOfOverflowing()
        {
            var idsBuffer = new NativeMemory<ulong>(IDs.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return idsBuffer;
        }
        
        public void GatherAndWidenAttentionMaskInclusiveOfOverflowing(
            MemoryWindow<ulong> attentionMaskBuffer,
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<ulong> GatherAndWidenAttentionMaskInclusiveOfOverflowing()
        {
            var attentionMaskBuffer = new NativeMemory<ulong>(AttentionMask.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return attentionMaskBuffer;
        }
        
        public void GatherAndWidenSpecialTokensMaskInclusiveOfOverflowing(
            MemoryWindow<ulong> specialTokensMaskBuffer,
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<ulong> GatherAndWidenSpecialTokensMaskInclusiveOfOverflowing()
        {
            var specialTokensMaskBuffer = new NativeMemory<ulong>(SpecialTokensMask.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return specialTokensMaskBuffer;
        }
        
        public void GatherAndWidenTokenTypeIDsInclusiveOfOverflowing(
            MemoryWindow<ulong> tokenTypeIDsBuffer,
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessTokenTypeIDs>(
                tokenTypeIDsBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<ulong> GatherAndWidenTokenTypeIDsInclusiveOfOverflowing()
        {
            var tokenTypeIDsBuffer = new NativeMemory<ulong>(TokenTypeIDs.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessTokenTypeIDs>(
                tokenTypeIDsBuffer.Window,
                performRangeCheck: false,
                out _
            );
            
            return tokenTypeIDsBuffer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GatherAndWidenIDsInclusiveOfOverflowingCore<FieldAccessorT>(
            MemoryWindow<ulong> idsBuffer, 
            bool performRangeCheck,
            out nuint totalLength)
            where FieldAccessorT: struct, IGatherFieldAccessor
        {
            var sourceBuffer = FieldAccessorT.AccessField(this);

            var overflowingTokens = OverflowingTokens;
            
            var segmentLength = (int) sourceBuffer.Length;

            var numSegments = overflowingTokens.Length + 1;
            
            totalLength = numSegments * (nuint) segmentLength;
            
            if (performRangeCheck)
            {
                if (idsBuffer.Length < totalLength)
                {
                    throw new ArgumentException("The provided buffer is too small.");
                }
            }
            
            var currentDestPtr = idsBuffer.Ptr;

            var currentSourceBuffer = sourceBuffer;

            var enumerator = overflowingTokens.AsReadOnlySpan().GetEnumerator();

            while (true)
            {
                currentSourceBuffer.WidenUnsafely(new(currentDestPtr, (nuint) segmentLength));
                
                currentDestPtr += segmentLength;
                
                if (enumerator.MoveNext())
                {
                    currentSourceBuffer = FieldAccessorT.AccessField(enumerator.Current);
                    continue;
                }
                
                return;
            }
        }
    }

    // Dispose
    public readonly unsafe partial struct TokenizeOutput
    {
        private const int NUM_HANDLES = 2;
        
        [InlineArray(NUM_HANDLES)]
        private struct DisposeFixedBuffer
        {
            public nint First;

            [Obsolete("Use constructor with parameters.", error: true)]
            public DisposeFixedBuffer()
            {
                throw new NotSupportedException("Use constructor with parameters.");
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DisposeFixedBuffer(nint handle1, nint handle2)
            {
                First = handle1;
                this[1] = handle2;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public nint* AsPointerUnsafely()
            {
                return (nint*) Unsafe.AsPointer(ref First);
            }
        }
        
        [SkipLocalsInit]
        // May be used in hot paths, where results are read and discarded
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var originalOutputFreeHandle = OriginalOutputFreeHandle;
            var overflowingTokensHandle = OverflowingTokensFreeHandle;

            var freeOverflowingTokens = overflowingTokensHandle != nint.Zero;

            // The order here is important, as we truncate the length based on freeOverflowingTokens
            var buffer = new DisposeFixedBuffer(originalOutputFreeHandle, overflowingTokensHandle);
                
            TokenizerNativeMethods.FreeWithMultipleHandles(new(
                buffer.AsPointerUnsafely(), 
                length: freeOverflowingTokens ? (nuint) 2 : 1)
            );
        }
    }
}