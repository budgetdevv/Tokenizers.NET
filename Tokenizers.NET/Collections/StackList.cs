using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Collections
{
    public unsafe ref struct StackList<T>: IDisposable
        where T : unmanaged
    {
        private T* Ptr;
        
        private int Capacity;
        
        public int Count { get; private set; }

        private bool IsInitialMemory;

        public StackList()
        {
            throw new NotSupportedException();
        }
        
        // Caller is responsible for freeing underlying memory of pinnedSpan
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StackList(Span<T> pinnedSpan)
        {
            Ptr = (T*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(pinnedSpan));

            Count = 0;
            
            Capacity = pinnedSpan.Length;
            
            IsInitialMemory = true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            var writeIndex = Count++;

            while (true)
            {
                // Do NOT store Capacity and Ptr as locals, as they can change after a resize
                
                if (writeIndex < Capacity)
                {
                    Ptr[writeIndex] = item;
                    return;
                }
                
                Resize();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Resize()
        {
            var oldCapacity = Capacity;

            var newCapacity = (nuint) (Capacity = (oldCapacity * 2));
            
            var sizeOfT = (nuint) sizeof(T);
            
            var oldPtr = Ptr;

            if (IsInitialMemory)
            {
                var newPtr = (T*) NativeMemory.Alloc(elementCount: newCapacity, elementSize: sizeOfT);
                
                NativeMemory.Copy(oldPtr, newPtr, (nuint) oldCapacity * sizeOfT);
                
                Ptr = newPtr;
                
                IsInitialMemory = false;
            }

            else
            {
                Ptr = (T*) NativeMemory.Realloc(oldPtr, byteCount: newCapacity * (nuint) sizeof(T));
            }
        }

        public Span<T> AsSpan()
        {
            return new(Ptr, Count);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }
        
        public void Dispose()
        {
            if (!IsInitialMemory)
            {
                NativeMemory.Free(Ptr);
            }
        }
    }
}