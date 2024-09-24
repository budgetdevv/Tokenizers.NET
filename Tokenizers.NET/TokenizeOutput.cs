using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    public interface ITokenizeOutput
    {
        public ReadOnlyNativeBuffer<uint> IDs { get; }
        
        public ReadOnlyNativeBuffer<uint> AttentionMask { get; }
        
        public ReadOnlyNativeBuffer<uint> SpecialTokensMask { get; }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TokenizeOutputOverflowedToken: ITokenizeOutput
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.IDs => IDs;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.AttentionMask => AttentionMask;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.SpecialTokensMask => SpecialTokensMask;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct TokenizeOutput: ITokenizeOutput, IDisposable
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;

        public readonly ReadOnlyNativeBuffer<TokenizeOutputOverflowedToken> OverflowingTokens;

        public readonly nint OriginalOutputFreeHandle;
        
        private readonly nint OverflowingTokensFreeHandle;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.IDs => IDs;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.AttentionMask => AttentionMask;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.SpecialTokensMask => SpecialTokensMask;
        
        private interface IGatherFieldAccessor
        {
            public static abstract ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput;
        }

        private struct AccessIDs: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.IDs;
            }
        }
        
        private struct AccessAttentionMask: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.AttentionMask;
            }
        }
        
        private struct AccessSpecialTokensMask: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.SpecialTokensMask;
            }
        }

        public void GatherIDsInclusiveOfOverflowing(NativeBuffer<uint> idsBuffer, bool performRangeCheck)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessIDs>(idsBuffer, performRangeCheck);
        }
        
        public void GatherAttentionMaskInclusiveOfOverflowing(NativeBuffer<uint> attentionMaskBuffer, bool performRangeCheck)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessAttentionMask>(attentionMaskBuffer, performRangeCheck);
        }
        
        public void GatherSpecialTokensMaskInclusiveOfOverflowing(NativeBuffer<uint> specialTokensMaskBuffer, bool performRangeCheck)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(specialTokensMaskBuffer, performRangeCheck);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GatherIDsInclusiveOfOverflowingCore<FieldAccesorT>(
            NativeBuffer<uint> idsBuffer, 
            bool performRangeCheck)
            where FieldAccesorT: struct, IGatherFieldAccessor
        {
            var sourceBuffer = FieldAccesorT.AccessField(this);

            var overflowingTokens = OverflowingTokens;
            
            var segmentLength = (int) sourceBuffer.Length;

            var numSegments = overflowingTokens.Length + 1;
            
            if (performRangeCheck)
            {
                if (idsBuffer.Length < (numSegments * (nuint) segmentLength))
                {
                    throw new ArgumentException("The provided buffer is too small.");
                }
            }

            var currentDstPtr = idsBuffer.Ptr;
            
            var sourceSpan = sourceBuffer.AsReadOnlySpan();

            var enumerator = overflowingTokens.AsReadOnlySpan().GetEnumerator();

            while (true)
            {
                sourceSpan.CopyTo(new(currentDstPtr, segmentLength));
                
                currentDstPtr += segmentLength;
                
                if (enumerator.MoveNext())
                {
                    sourceSpan = FieldAccesorT.AccessField(enumerator.Current).AsReadOnlySpan();
                    continue;
                }
                
                return;
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
            
            #if DEBUG
            TokenizerNativeMethods.FreeWithHandle(originalOutputFreeHandle);
            
            if (freeOverflowingTokens)
            {
                TokenizerNativeMethods.FreeWithHandle(overflowingTokensHandle);
            }
            
            #else
            // It is fine to over-allocate
            var ptr = stackalloc nint[2];
            
            *ptr = originalOutputFreeHandle;
            *(ptr + 1) = overflowingTokensHandle;
                
            TokenizerNativeMethods.FreeWithMultipleHandles(new(
                ptr, 
                length: freeOverflowingTokens ? (nuint) 2 : 1)
            );
            #endif
        }
    }
}