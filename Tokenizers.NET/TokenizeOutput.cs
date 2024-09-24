using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TokenizeOutputOverflowedToken
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct TokenizeOutput: IDisposable
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;

        public readonly ReadOnlyNativeBuffer<TokenizeOutputOverflowedToken> OverflowingTokens;

        public readonly nint OriginalOutputFreeHandle;
        
        private readonly nint OverflowingTokensFreeHandle;

        public void GatherIDsInclusiveOfOverflowing(NativeBuffer<uint> idsBuffer, bool performRangeCheck)
        {
            var ids = IDs;

            var overflowingTokens = OverflowingTokens;
            
            var segmentLength = (int) ids.Length;

            var numSegments = overflowingTokens.Length + 1;
            
            if (performRangeCheck)
            {
                if (idsBuffer.Length < (numSegments * (nuint) segmentLength))
                {
                    throw new ArgumentException("The provided buffer is too small to hold all the IDs.");
                }
            }

            var currentDstPtr = idsBuffer.Ptr;
            
            var sourceSpan = ids.AsReadOnlySpan();

            var enumerator = overflowingTokens.AsReadOnlySpan().GetEnumerator();

            while (true)
            {
                sourceSpan.CopyTo(new(currentDstPtr, segmentLength));
                
                currentDstPtr += segmentLength;
                
                if (enumerator.MoveNext())
                {
                    sourceSpan = enumerator.Current.IDs.AsReadOnlySpan();
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