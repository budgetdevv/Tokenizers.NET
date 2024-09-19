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
    }
}