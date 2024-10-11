using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Helpers
{
    internal static unsafe class AllocationHelpers
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
        
        // Note that the array reference itself may not point to an aligned address.
        public static T[] AllocatePinnedUninitializedAligned<T>(
            int length,
            int alignment,
            out T* alignedPtr)
            where T : unmanaged
        {
            var extraAllocs = (alignment / sizeof(T)) + 1;

            #if DEBUG
            if (alignment % sizeof(T) != 0)
            {
                throw new Exception("Invalid alignment!");
            }
            #endif
            
            var arr = AllocatePinnedUninitialized<T>(length + extraAllocs);
            
            var addr = (nint) arr.PinnedArrayToPointer();

            var offset = addr % alignment;
            
            if (offset != 0)
            {
                addr += (alignment - offset);
            }
            
            #if DEBUG
            Debug.Assert(addr % alignment == 0);
            #endif
            
            alignedPtr = (T*) addr;
            
            return arr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* PinnedArrayToPointer<T>(this T[] arr) where T: unmanaged
        {
            return (T*) Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(arr));
        }
    }
}