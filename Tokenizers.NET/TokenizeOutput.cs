using System;
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
    public readonly struct TokenizeOutput: IDisposable
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;

        public readonly ReadOnlyNativeBuffer<TokenizeOutputOverflowedToken> OverflowingTokens;

        public readonly nint OriginalOutputFreeHandle;
            
        public void Dispose()
        {
            TokenizerNativeMethods.FreeWithHandle(OriginalOutputFreeHandle);
        }
    }
}