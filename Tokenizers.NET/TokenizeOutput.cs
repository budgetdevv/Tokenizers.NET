using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    public interface ITokenizeOutput
    {
        public ReadOnlyNativeBuffer<uint> IDs { get; }
        
        public ReadOnlyNativeBuffer<uint> AttentionMask { get; }
        
        public ReadOnlyNativeBuffer<uint> SpecialTokensMask { get; }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TokenizeOutputOverflowedToken: ITokenizeOutput
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.IDs => IDs;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.AttentionMask => AttentionMask;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.SpecialTokensMask => SpecialTokensMask;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct TokenizeOutput: ITokenizeOutput, IDisposable
    {
        public readonly ReadOnlyNativeBuffer<uint> IDs;

        public readonly ReadOnlyNativeBuffer<uint> AttentionMask;

        public readonly ReadOnlyNativeBuffer<uint> SpecialTokensMask;

        public readonly ReadOnlyNativeBuffer<TokenizeOutputOverflowedToken> OverflowingTokens;

        public readonly nint OriginalOutputFreeHandle;
        
        private readonly nint OverflowingTokensFreeHandle;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.IDs => IDs;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.AttentionMask => AttentionMask;
        
        ReadOnlyNativeBuffer<uint> ITokenizeOutput.SpecialTokensMask => SpecialTokensMask;
        
        private interface IGatherFieldAccessor
        {
            public static abstract ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput;
        }

        private struct AccessIDs: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.IDs;
            }
        }
        
        private struct AccessAttentionMask: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.AttentionMask;
            }
        }
        
        private struct AccessSpecialTokensMask: IGatherFieldAccessor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ReadOnlyNativeBuffer<uint> AccessField<T>(T item)
                where T: struct, ITokenizeOutput
            {
                return item.SpecialTokensMask;
            }
        }

        public void GatherIDsInclusiveOfOverflowing(
            NativeBuffer<uint> idsBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherIDsInclusiveOfOverflowing()
        {
            var idsBuffer = new NativeMemory<uint>(IDs.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer.Buffer,
                performRangeCheck: false,
                out _
            );
            
            return idsBuffer;
        }
        
        public void GatherAttentionMaskInclusiveOfOverflowing(
            NativeBuffer<uint> attentionMaskBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer, 
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherAttentionMaskInclusiveOfOverflowing()
        {
            var attentionMaskBuffer = new NativeMemory<uint>(AttentionMask.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer.Buffer,
                performRangeCheck: false,
                out _
            );
            
            return attentionMaskBuffer;
        }
        
        public void GatherSpecialTokensMaskInclusiveOfOverflowing(
            NativeBuffer<uint> specialTokensMaskBuffer,
            out nuint totalLength)
        {
            GatherIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<uint> GatherSpecialTokensMaskInclusiveOfOverflowing()
        {
            var specialTokensMaskBuffer = new NativeMemory<uint>(SpecialTokensMask.Length * (OverflowingTokens.Length + 1));
            
            GatherIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer.Buffer,
                performRangeCheck: false,
                out _
            );
            
            return specialTokensMaskBuffer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GatherIDsInclusiveOfOverflowingCore<FieldAccessorT>(
            NativeBuffer<uint> idsBuffer, 
            bool performRangeCheck,
            out nuint totalLength)
            where FieldAccessorT: struct, IGatherFieldAccessor
        {
            var sourceBuffer = FieldAccessorT.AccessField(this);

            var overflowingTokens = OverflowingTokens;
            
            var segmentLength = (int) sourceBuffer.Length;

            var numSegments = overflowingTokens.Length + 1;
            
            totalLength = numSegments * (nuint) segmentLength;
            
            if (performRangeCheck)
            {
                if (idsBuffer.Length < totalLength)
                {
                    throw new ArgumentException("The provided buffer is too small.");
                }
            }

            var currentDestPtr = idsBuffer.Ptr;
            
            var currentSourceSpan = sourceBuffer.AsReadOnlySpan();

            var enumerator = overflowingTokens.AsReadOnlySpan().GetEnumerator();

            while (true)
            {
                currentSourceSpan.CopyTo(new(currentDestPtr, segmentLength));
                
                currentDestPtr += segmentLength;
                
                if (enumerator.MoveNext())
                {
                    currentSourceSpan = FieldAccessorT.AccessField(enumerator.Current).AsReadOnlySpan();
                    continue;
                }
                
                return;
            }
        }

        public void GatherAndWidenIDsInclusiveOfOverflowing(
            NativeBuffer<ulong> idsBuffer, 
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }

        public NativeMemory<ulong> GatherAndWidenIDsInclusiveOfOverflowing()
        {
            var idsBuffer = new NativeMemory<ulong>(IDs.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessIDs>(
                idsBuffer.Buffer,
                performRangeCheck: false,
                out _
            );
            
            return idsBuffer;
        }
        
        public void GatherAndWidenAttentionMaskInclusiveOfOverflowing(
            NativeBuffer<ulong> attentionMaskBuffer,
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<ulong> GatherAndWidenAttentionMaskInclusiveOfOverflowing()
        {
            var attentionMaskBuffer = new NativeMemory<ulong>(AttentionMask.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessAttentionMask>(
                attentionMaskBuffer.Buffer,
                performRangeCheck: false,
                out _
            );
            
            return attentionMaskBuffer;
        }
        
        public void GatherAndWidenSpecialTokensMaskInclusiveOfOverflowing(
            NativeBuffer<ulong> specialTokensMaskBuffer,
            out nuint totalLength)
        {
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer,
                performRangeCheck: true,
                out totalLength
            );
        }
        
        public NativeMemory<ulong> GatherAndWidenSpecialTokensMaskInclusiveOfOverflowing()
        {
            var specialTokensMaskBuffer = new NativeMemory<ulong>(SpecialTokensMask.Length * (OverflowingTokens.Length + 1));
            
            GatherAndWidenIDsInclusiveOfOverflowingCore<AccessSpecialTokensMask>(
                specialTokensMaskBuffer.Buffer,
                performRangeCheck: false,
                out _
            );
            
            return specialTokensMaskBuffer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GatherAndWidenIDsInclusiveOfOverflowingCore<FieldAccessorT>(
            NativeBuffer<ulong> idsBuffer, 
            bool performRangeCheck,
            out nuint totalLength)
            where FieldAccessorT: struct, IGatherFieldAccessor
        {
            var sourceBuffer = FieldAccessorT.AccessField(this);

            var overflowingTokens = OverflowingTokens;
            
            var segmentLength = (int) sourceBuffer.Length;

            var numSegments = overflowingTokens.Length + 1;
            
            totalLength = numSegments * (nuint) segmentLength;
            
            if (performRangeCheck)
            {
                if (idsBuffer.Length < totalLength)
                {
                    throw new ArgumentException("The provided buffer is too small.");
                }
            }
            
            var currentDestPtr = idsBuffer.Ptr;

            var currentSourceBuffer = sourceBuffer;

            var enumerator = overflowingTokens.AsReadOnlySpan().GetEnumerator();

            while (true)
            {
                currentSourceBuffer.WidenUnsafely(new(currentDestPtr, (nuint) segmentLength));
                
                currentDestPtr += segmentLength;
                
                if (enumerator.MoveNext())
                {
                    currentSourceBuffer = FieldAccessorT.AccessField(enumerator.Current);
                    continue;
                }
                
                return;
            }
        }

        private const int NUM_HANDLES = 2;
        
        [InlineArray(NUM_HANDLES)]
        private struct DisposeFixedBuffer
        {
            public nint First;

            [Obsolete("Use constructor with parameters.", error: true)]
            public DisposeFixedBuffer()
            {
                throw new NotSupportedException("Use constructor with parameters.");
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DisposeFixedBuffer(nint handle1, nint handle2)
            {
                First = handle1;
                this[1] = handle2;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public nint* AsPointerUnsafely()
            {
                return (nint*) Unsafe.AsPointer(ref First);
            }
        }
        
        [SkipLocalsInit]
        // May be used in hot paths, where results are read and discarded
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var originalOutputFreeHandle = OriginalOutputFreeHandle;
            var overflowingTokensHandle = OverflowingTokensFreeHandle;

            var freeOverflowingTokens = overflowingTokensHandle != nint.Zero;

            // The order here is important, as we truncate the length based on freeOverflowingTokens
            var buffer = new DisposeFixedBuffer(originalOutputFreeHandle, overflowingTokensHandle);
                
            TokenizerNativeMethods.FreeWithMultipleHandles(new(
                buffer.AsPointerUnsafely(), 
                length: freeOverflowingTokens ? (nuint) 2 : 1)
            );
        }
    }
}