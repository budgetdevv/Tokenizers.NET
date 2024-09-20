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
            
            if (overflowingTokensHandle == nint.Zero)
            {
                TokenizerNativeMethods.FreeWithHandle(originalOutputFreeHandle);
            }

            else
            {
                #if DEBUG
                TokenizerNativeMethods.FreeWithHandle(originalOutputFreeHandle);
                TokenizerNativeMethods.FreeWithHandle(overflowingTokensHandle);
                #else
                var ptr = stackalloc nint[2];
                
                *ptr = originalOutputFreeHandle;
                *(ptr + 1) = overflowingTokensHandle;
                
                TokenizerNativeMethods.FreeWithMultipleHandles(new(ptr, 2));
                #endif
            }
        }
    }
}