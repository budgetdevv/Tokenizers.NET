using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokenizers.NET.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct NativeMemory<T>: IDisposable, IEquatable<NativeMemory<T>>
        where T: unmanaged
    {
        #if DEBUG
        // Ptr to size
        private static readonly ConcurrentDictionary<nint, nuint> LIVE_ALLOCATIONS = new();
        
        public static int LiveAllocationsCount => LIVE_ALLOCATIONS.Count;
        #endif
        
        public readonly NativeBuffer<T> Buffer;

        [Obsolete("Use constructor with parameters.", error: true)]
        public NativeMemory()
        {
            throw new NotSupportedException("Use constructor with parameters.");
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public NativeMemory(nuint length, bool zeroed = false)
        {
            var size = (nuint) sizeof(T);

            T* ptr;

            if (!zeroed)
            {
                ptr = (T*) NativeMemory.Alloc(length, size);
            }

            else
            {
                ptr = (T*) NativeMemory.AllocZeroed(length, size);
            }
            
            Buffer = new(ptr, length);
            
            #if DEBUG
            Debug.Assert(LIVE_ALLOCATIONS.TryAdd((nint) ptr, size));
            #endif
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeWithPtrUnsafely(T* ptr)
        {
            #if DEBUG
            // ReSharper disable once UnusedVariable
            // We keep the size variable even though it is useless,
            // so that we can view the size via the debugger!
            Debug.Assert(LIVE_ALLOCATIONS.TryRemove((nint) ptr, out var size));
            #endif
            
            NativeMemory.Free(ptr);
        }
        
        public void Dispose()
        {
            FreeWithPtrUnsafely(Buffer.Ptr);
        }
    }
}