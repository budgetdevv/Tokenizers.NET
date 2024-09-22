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
            bool truncate)
        {
            if (truncate)
            {
                return TokenizerEncode(tokenizerPtr, textNativeBuffer);
            }

            else
            {
                return TokenizerEncodeNonTruncating(tokenizerPtr, textNativeBuffer);
            }
        }
        
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TokenizeOutput TokenizerEncode(nint tokenizerPtr, ReadOnlyNativeBuffer<byte> textNativeBuffer);
        
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_non_truncating", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TokenizeOutput TokenizerEncodeNonTruncating(nint tokenizerPtr, ReadOnlyNativeBuffer<byte> textNativeBuffer);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            bool truncate)
        {
            if (truncate)
            {
                TokenizerEncodeBatch(tokenizerPtr, textNativeBuffers, outputNativeBuffer);
            }
            
            else
            {
                TokenizerEncodeBatchNonTruncating(tokenizerPtr, textNativeBuffers, outputNativeBuffer);
            }
        }
        
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer
        );
        
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch_non_truncating", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void TokenizerEncodeBatchNonTruncating(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer
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
        
        [DllImport(DLL_NAME, EntryPoint = "tokenizer_decode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern DecodeOutput TokenizerDecode(nint tokenizerPtr, ReadOnlyNativeBuffer<uint> idBuffer);

        [DllImport(DLL_NAME, EntryPoint = "tokenizer_decode_skip_special_tokens", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern DecodeOutput TokenizerDecodeSkipSpecialTokens(nint tokenizerPtr, ReadOnlyNativeBuffer<uint> idBuffer);

        [DllImport(DLL_NAME, EntryPoint = "free_with_handle", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void FreeWithHandle(nint handle);
        
        [DllImport(DLL_NAME, EntryPoint = "free_with_multiple_handles", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void FreeWithMultipleHandles(ReadOnlyNativeBuffer<nint> handles);
    }
}