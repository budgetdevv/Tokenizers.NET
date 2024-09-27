using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    internal static unsafe class TokenizerNativeMethods
    {
        private const string DLL_NAME = "tokenizers_net";

        [DllImport(DLL_NAME, EntryPoint = "allocate_tokenizer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern nint AllocateTokenizer(byte* jsonBytesPtr, nuint jsonBytesLength);
        
        [DllImport(DLL_NAME, EntryPoint = "free_tokenizer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern nint FreeTokenizer(nint tokenizerHandle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<byte> textNativeBuffer,
            bool addSpecialTokens,
            bool truncate)
        {
            if (truncate)
            {
                return TokenizerEncode(tokenizerPtr, textNativeBuffer, addSpecialTokens);
            }

            else
            {
                return TokenizerEncodeNonTruncating(tokenizerPtr, textNativeBuffer, addSpecialTokens);
            }
        }
        
        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<byte> textNativeBuffer,
            [MarshalAs(UnmanagedType.U1)] bool addSpecialTokens
        );
        
        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_non_truncating", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TokenizeOutput TokenizerEncodeNonTruncating(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<byte> textNativeBuffer,
            [MarshalAs(UnmanagedType.U1)] bool addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            bool addSpecialTokens,
            bool truncate)
        {
            if (truncate)
            {
                TokenizerEncodeBatch(tokenizerPtr, textNativeBuffers, outputNativeBuffer, addSpecialTokens);
            }
            
            else
            {
                TokenizerEncodeBatchNonTruncating(tokenizerPtr, textNativeBuffers, outputNativeBuffer, addSpecialTokens);
            }
        }
        
        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            [MarshalAs(UnmanagedType.U1)] bool addSpecialTokens
        );
        
        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch_non_truncating", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void TokenizerEncodeBatchNonTruncating(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            [MarshalAs(UnmanagedType.U1)] bool addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DecodeOutput TokenizerDecode(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<uint> ids,
            bool skipSpecialTokens)
        {
            if (skipSpecialTokens)
            {
                return TokenizerDecodeSkipSpecialTokens(tokenizerPtr, ids);
            }

            else
            {
                return TokenizerDecode(tokenizerPtr, ids);
            }
        }
        
        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_decode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern DecodeOutput TokenizerDecode(nint tokenizerPtr, ReadOnlyNativeBuffer<uint> idBuffer);

        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_decode_skip_special_tokens", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern DecodeOutput TokenizerDecodeSkipSpecialTokens(nint tokenizerPtr, ReadOnlyNativeBuffer<uint> idBuffer);

        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "free_with_handle", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void FreeWithHandle(nint handle);
        
        [SuppressGCTransition]
        [DllImport(DLL_NAME, EntryPoint = "free_with_multiple_handles", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void FreeWithMultipleHandles(ReadOnlyNativeBuffer<nint> handles);
    }
}