using System.Runtime.CompilerServices;

namespace Tokenizers.NET.Helpers
{
    internal static class IntegerHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToSignedUnchecked(this uint value)
        {
            return unchecked((int) value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUnsignedUnchecked(this int value)
        {
            return unchecked((uint) value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ToNuintUnchecked(this int value)
        {
            return unchecked((nuint) value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ToNintUnchecked(this uint value)
        {
            return unchecked((nint) value);
        }
    }
}