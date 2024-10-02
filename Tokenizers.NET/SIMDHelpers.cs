using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    public static unsafe class SIMDHelpers
    {
        public static NativeMemory<ulong> Widen(this NativeBuffer<uint> srcBuffer)
        {
            var result = new NativeMemory<ulong>(srcBuffer.Length);
            
            srcBuffer.WidenInternal(result.Buffer, performLengthCheck: false);
            
            return result;
        }
        
        public static void Widen(this NativeBuffer<uint> srcBuffer, NativeBuffer<ulong> destBuffer)
        {
            srcBuffer.WidenInternal(destBuffer, performLengthCheck: true);
        }
        
        public static void WidenUnsafely(this NativeBuffer<uint> srcBuffer, NativeBuffer<ulong> destBuffer)
        {
            srcBuffer.WidenInternal(destBuffer, performLengthCheck: false);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenInternal(
            this NativeBuffer<uint> srcBuffer,
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
                goto Scalar;
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
            
            Scalar:
            for (; currentSrcPtr < lastSrcOffsetByOne; currentSrcPtr++, currentDestPtr++)
            {
                *currentDestPtr = *currentSrcPtr;
            }
        }

        public static NativeBuffer<uint> NarrowMutating(this NativeBuffer<ulong> srcBuffer)
        {
            var reinterpreted = srcBuffer.Cast<uint>();
            
            NarrowInternal(
                srcBuffer,
                destBuffer: reinterpreted,
                performLengthCheck: false,
                overlapping: true
            );

            return reinterpreted;
        }
        
        public static void NarrowNonOverlapping(
            this NativeBuffer<ulong> srcBuffer,
            NativeBuffer<uint> destBuffer)
        {
            NarrowInternal(
                srcBuffer,
                destBuffer,
                performLengthCheck: true,
                overlapping: false
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NarrowInternal(
            this NativeBuffer<ulong> srcBuffer,
            NativeBuffer<uint> destBuffer,
            bool performLengthCheck,
            bool overlapping)
        {
            if (performLengthCheck)
            {
                if (srcBuffer.Length > destBuffer.Length)
                {
                    throw new ArgumentException("Destination buffer is too small.");
                }
            }
            
            var firstSrcPtr = srcBuffer.Ptr;
            var firstDestPtr = destBuffer.Ptr;
            
            var currentSrcPtr = firstSrcPtr;
            var currentDestPtr = firstDestPtr;
            
            var srcLength = srcBuffer.Length;
            
            var lastSrcOffsetByOne = currentSrcPtr + srcLength;
            
            // Vector<T>.Count is JIT intrinsic, so don't hoist it as a local
            
            if (srcLength < (nuint) Vector<uint>.Count)
            {
                goto Scalar;
            }
            
            var srcLengthTruncated = srcLength & ~((nuint) Vector<uint>.Count - 1);
            
            for (ulong* lastSrcInTruncatedOffsetByOne = currentSrcPtr + srcLengthTruncated;
                 currentSrcPtr < lastSrcInTruncatedOffsetByOne;
                 currentSrcPtr += Vector<uint>.Count, 
                 currentDestPtr += Vector<uint>.Count)
            {
                var srcVecLow = Vector.Load(currentSrcPtr);
                var srcVecHigh = Vector.Load(currentSrcPtr + Vector<ulong>.Count);

                var narrowedVec = Vector.Narrow(srcVecLow, srcVecHigh);
                
                narrowedVec.Store(currentDestPtr);
            }

            if (srcLength != srcLengthTruncated)
            {
                // Handle remaining
                
                // Assume Vector<uint>.Count is 3
                // [ 0, 1, 2 ] 3 -> 3 - 3 = 0, which gives us the start of the last vec
                
                var lastSrcVecStart = lastSrcOffsetByOne - Vector<uint>.Count;

                var diff = currentSrcPtr - lastSrcVecStart;
                
                #if DEBUG
                Debug.Assert(diff > 0);
                #endif
                
                var lastDestVecStart = currentDestPtr - diff;

                if (overlapping)
                {
                    if (Avx2.IsSupported)
                    {
                        // Load the last source vec
                        var lastSrcVecLow = Vector256.Load(lastSrcVecStart);
                        var lastSrcVecHigh = Vector256.Load(lastSrcVecStart + Vector<ulong>.Count);
                    
                        var lastNarrowedVec = Vector256.Narrow(lastSrcVecLow, lastSrcVecHigh);
                
                        var lastDestVec = Vector256.Load(lastDestVecStart);
                    
                        // The last diff number of LBS-s are zeroed, the rest are 1
                        // https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA0AXEUCuA7AHwAEAmARgFgAoa/MACxjAGsYATACg+AE8MYAlNz4wAdAFkAhggBqkgDY4YAAgA8q5QGYBA0QAUoASzwYAQsclQeHAQG5qDqkU3KiZJK5LLzeSzwASMPIADjBQAM7UAN7UynGuLm4eRCjKBsZmFlYcGPSG4cq8/MoAknjBOBgCsfExVPENrmQAnBwAwhB4AG5hGKIAKhAAyhhGeADmHGUVGGjKJLp6kmwAMjAAZhgcABxzAOQADHs69vXxAL7U50A=
                        var mask = unchecked((byte) (byte.MaxValue << (int) diff));

                        // Imagine diff is 3, we get 11111000
                        // The mask reads from LSB
                        // So for the first 3 elements, we take from lastDestVec ( The original value )
                        // And the remaining we take from lastNarrowedVec ( The narrowed value )
                        lastNarrowedVec = Avx2.Blend(
                            lastDestVec, // Clear bit means take from this vec
                            lastNarrowedVec, // Set bit means take from this vec
                            mask
                        );
                    
                        lastNarrowedVec.Store(lastDestVecStart);
                    }
                
                    else
                    {
                        // Not really worth it I believe, just fallback to scalar
                        currentSrcPtr = firstSrcPtr + srcLengthTruncated;
                        currentDestPtr = firstDestPtr + srcLengthTruncated;
                        goto Scalar;
                    }
                }

                else
                {
                    // Load the last source vec
                    var lastSrcVecLow = Vector.Load(lastSrcVecStart);
                    var lastSrcVecHigh = Vector.Load(lastSrcVecStart + Vector<ulong>.Count);
                    
                    var lastNarrowedVec = Vector.Narrow(lastSrcVecLow, lastSrcVecHigh);
                
                    lastNarrowedVec.Store(lastDestVecStart);
                }
            }
            
            return;
            
            Scalar:
            for (; currentSrcPtr < lastSrcOffsetByOne; currentSrcPtr++, currentDestPtr++)
            {
                *currentDestPtr = unchecked((uint) *currentSrcPtr);
            }
        }
    }
}