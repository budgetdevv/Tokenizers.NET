using System;
using System.Runtime.CompilerServices;

namespace Tokenizers.NET.Outputs
{
    public readonly struct FreeHandle(nint handle): IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            TokenizerNativeMethods.FreeWithHandle(handle);
        }
    }
}