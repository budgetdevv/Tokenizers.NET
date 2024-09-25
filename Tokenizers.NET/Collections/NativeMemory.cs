using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct NativeMemory<T>: IDisposable, IEquatable<NativeMemory<T>>
        where T: unmanaged
    {
        public readonly NativeBuffer<T> Buffer;

        public NativeMemory()
        {
            throw new NotSupportedException();
        }
        
        public NativeMemory(nuint length)
        {
            var ptr = (T*) NativeMemory.Alloc(length, (nuint) sizeof(T));
            
            Buffer = new(ptr, length);
        }
        
        // NativeMemory<T> should have the exact same memory layout as NativeBuffer<T>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeMemory<T> WrapBuffer(NativeBuffer<T> buffer)
        {
            return Unsafe.BitCast<NativeBuffer<T>, NativeMemory<T>>(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NativeMemory<T> other)
        {
            var otherBuffer = other.Buffer;
            var buffer = Buffer;
            
            return otherBuffer.Ptr == buffer.Ptr && otherBuffer.Length == buffer.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is NativeMemory<T> other && Equals(other);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return (int) Buffer.Ptr;
        }

        public static bool operator ==(NativeMemory<T> left, NativeMemory<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativeMemory<T> left, NativeMemory<T> right)
        {
            return !left.Equals(right);
        }
        
        public void Dispose()
        {
            NativeMemory.Free(Buffer.Ptr);
        }
    }
}