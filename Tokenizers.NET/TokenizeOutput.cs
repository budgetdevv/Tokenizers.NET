using System;
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