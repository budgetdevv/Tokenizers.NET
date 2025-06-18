using System.Runtime.InteropServices;
using System.Text;
using NativeMemory;

namespace Tokenizers.NET.Outputs
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DecodeOutput: IDisposable
    {
        public readonly MemoryWindow<byte> TextBuffer;
        
        public readonly nint FreeHandle;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return Encoding.UTF8.GetString(TextBuffer.AsReadOnlySpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            TokenizerNativeMethods.FreeWithHandle(FreeHandle);
        }
    }
}