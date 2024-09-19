using System;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Collections
{
    public readonly unsafe struct NativeMemory<T>: IDisposable where T : unmanaged
    {
        public readonly NativeBuffer<T> Memory;

        public NativeMemory()
        {
            throw new NotSupportedException();
        }
        
        public NativeMemory(nuint length)
        {
            var ptr = (T*) NativeMemory.Alloc(length, (nuint) sizeof(T));
            
            Memory = new(ptr, length);
        }

        public void Dispose()
        {
            NativeMemory.Free(Memory.Ptr);
        }
    }
}