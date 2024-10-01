using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Helpers
{
    internal static class AllocationHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] AllocatePinnedUninitialized<T>(uint length)
        {
            return AllocatePinnedUninitialized<T>(length.ToSignedUnchecked());
        }
        
        public static T[] AllocatePinnedUninitialized<T>(int length)
        {
            return GC.AllocateUninitializedArray<T>(length, pinned: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* PinnedArrayToPointer<T>(this T[] arr) where T: unmanaged
        {
            return (T*) Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(arr));
        }
    }
}