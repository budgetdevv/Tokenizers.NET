using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Enumerators
{
    public ref struct UnsafeReadOnlySpanEnumerator<T>
    {
        private ref T CurrentItem;

        private readonly ref T LastItemOffsetByOne;
        
        public readonly ref T Current => ref CurrentItem;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeReadOnlySpanEnumerator(ReadOnlySpan<T> span)
        {
            ref var current = ref MemoryMarshal.GetReference(span);
            
            LastItemOffsetByOne = ref Unsafe.Add(ref current, span.Length);
            
            CurrentItem = ref Unsafe.Subtract(ref current, 1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ref var currentItem = ref CurrentItem = ref Unsafe.Add(ref CurrentItem, 1);

            return !Unsafe.AreSame(ref currentItem, ref LastItemOffsetByOne);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeReadOnlySpanEnumerator<T> GetEnumerator() => this;
    }
}