using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Helpers;

namespace Tokenizers.NET.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct NativeBuffer<T>(T* ptr, nuint length) where T: unmanaged
    {
        public readonly T* Ptr = ptr;
        public readonly nuint Length = length;
       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeBuffer(T[] pinnedBuffer, nuint length) :
            this(ref MemoryMarshal.GetArrayDataReference(pinnedBuffer), length)
        {
            // Nothing here
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeBuffer(Span<T> pinnedSpan) :
            this(ref MemoryMarshal.GetReference(pinnedSpan), (nuint) pinnedSpan.Length)
        {
            // Nothing here
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeBuffer(ref T pinnedStart, nuint length) :
            this((T*) Unsafe.AsPointer(ref pinnedStart), length)
        {
            // Nothing here
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan()
        {
            return MemoryMarshal.CreateSpan(ref *Ptr, (int) Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            return MemoryMarshal.CreateReadOnlySpan(ref *Ptr, (int) Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeBuffer<F> Cast<F>() where F: unmanaged
        {
            return new((F*) Ptr, UnsafeHelpers.CalculateCastLength<T, F>(Length));
        }
        
        public struct Enumerator
        {
            private T* CurrentPtr;

            private readonly T* LastPtrOffsetByOne;
            
            public ref T Current 
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref *CurrentPtr;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(T* ptr, nuint length)
            {
                LastPtrOffsetByOne = ptr + length;
                CurrentPtr = ptr - 1;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++CurrentPtr != LastPtrOffsetByOne;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new(Ptr, Length);
        }
    }
}