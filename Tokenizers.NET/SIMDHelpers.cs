using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    public static unsafe class SIMDHelpers
    {
        public static NativeMemory<ulong> Widen(this ReadOnlyNativeBuffer<uint> srcBuffer)
        {
            var result = new NativeMemory<ulong>(srcBuffer.Length);
            
            srcBuffer.WidenInternal(result.Memory, performLengthCheck: false);
            
            return result;
        }
        
        public static void Widen(this ReadOnlyNativeBuffer<uint> srcBuffer, NativeBuffer<ulong> destBuffer)
        {
            srcBuffer.WidenInternal(destBuffer, performLengthCheck: true);
        }
        
        public static void WidenUnsafely(this ReadOnlyNativeBuffer<uint> srcBuffer, NativeBuffer<ulong> destBuffer)
        {
            srcBuffer.WidenInternal(destBuffer, performLengthCheck: false);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenInternal(
            this ReadOnlyNativeBuffer<uint> srcBuffer,
            NativeBuffer<ulong> destBuffer,
            bool performLengthCheck)
        {
            var currentSrcPtr = srcBuffer.Ptr;
            var currentDestPtr = destBuffer.Ptr;
            
            var srcLength = srcBuffer.Length;
            var destLength = destBuffer.Length;
            
            var lastSrcOffsetByOne = currentSrcPtr + srcLength;

            if (performLengthCheck)
            {
                if (srcLength > destLength)
                {
                    throw new ArgumentException("Destination buffer is too small.");
                }                
            }
            
            // Vector<T>.Count is JIT intrinsic, so don't hoist it as a local
            
            if (srcLength < (nuint) Vector<uint>.Count)
            {
                goto Short;
            }
            
            var srcLengthTruncated = srcLength & ~((nuint) Vector<uint>.Count - 1);
            
            for (uint* lastSrcInTruncatedOffsetByOne = currentSrcPtr + srcLengthTruncated;
                 currentSrcPtr < lastSrcInTruncatedOffsetByOne;
                 currentSrcPtr += Vector<uint>.Count, 
                 currentDestPtr += Vector<uint>.Count)
            {
                var srcVec = Vector.Load(currentSrcPtr);
                
                Vector.Widen(srcVec, out var low, out var high);
                
                low.Store(currentDestPtr);
                high.Store(currentDestPtr + Vector<ulong>.Count);
            }

            if (srcLength != srcLengthTruncated)
            {
                // Handle remaining
                
                // Assume Vector<uint>.Count is 3
                // [ 0, 1, 2 ] 3 -> 3 - 3 = 0, which gives us the start of the last vec
                
                var lastSrcVecStart = lastSrcOffsetByOne - Vector<uint>.Count;
                
                #if DEBUG
                Debug.Assert((currentSrcPtr - lastSrcVecStart) > 0);
                #endif
                
                var lastDestVecStart = currentDestPtr - (currentSrcPtr - lastSrcVecStart);
                
                // Load the last source vec
                var lastSrcVec = Vector.Load(lastSrcVecStart);
                
                Vector.Widen(lastSrcVec, out var low, out var high);
                
                low.Store(lastDestVecStart);
                high.Store(lastDestVecStart + Vector<ulong>.Count);
            }
            
            return;
            
            Short:
            for (; currentSrcPtr < lastSrcOffsetByOne; currentSrcPtr++, currentDestPtr++)
            {
                *currentDestPtr = *currentSrcPtr;
            }
        }
    }
}