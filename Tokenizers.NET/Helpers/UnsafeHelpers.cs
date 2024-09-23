using System.Runtime.CompilerServices;

namespace Tokenizers.NET.Helpers
{
    public static unsafe class UnsafeHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint CalculateCastLength<T, F>(nuint length)
            where T: unmanaged
            where F: unmanaged
        {
            // Adapted from MemoryMarshal.Cast()
            
            var fromSize = (nuint) sizeof(T);
            var toSize = (nuint) sizeof(F);

            var fromLength = length;
            
            nuint toLength;
            
            if (fromSize == toSize)
            {
                toLength = fromLength;
            }
            
            else if (fromSize == 1)
            {
                toLength = (fromLength / toSize);
            }
            
            else
            {
                toLength = fromLength * fromSize / toSize;
            }
            
            return toLength;
        }
    }
}