using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Collections;
using Tokenizers.NET.Outputs;

namespace Tokenizers.NET
{
    internal static unsafe partial class TokenizerNativeMethods
    {
        private const string DLL_NAME = "tokenizers_net";

        [LibraryImport(DLL_NAME, EntryPoint = "allocate_tokenizer")]
        public static partial nint AllocateTokenizer(NativeBuffer<byte> jsonBytes);
        
        [LibraryImport(DLL_NAME, EntryPoint = "free_tokenizer")]
        public static partial void FreeTokenizer(nint tokenizerHandle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            NativeBuffer<byte> textNativeBuffer,
            bool addSpecialTokens,
            bool truncate)
        {
            // https://github.com/rust-lang/reference/blob/master/src/behavior-considered-undefined.md
            // "A bool value must be false (0) or true (1)."
            var addSpecialTokensByte = unchecked((byte) (addSpecialTokens ? 1 : 0));
            
            if (truncate)
            {
                return TokenizerEncode(tokenizerPtr, textNativeBuffer, addSpecialTokensByte);
            }

            else
            {
                return TokenizerEncodeNonTruncating(tokenizerPtr, textNativeBuffer, addSpecialTokensByte);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode")]
        private static partial TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            NativeBuffer<byte> textNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_non_truncating")]
        private static partial TokenizeOutput TokenizerEncodeNonTruncating(
            nint tokenizerPtr,
            NativeBuffer<byte> textNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            NativeBuffer<NativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            bool addSpecialTokens,
            bool truncate)
        {
            // https://github.com/rust-lang/reference/blob/master/src/behavior-considered-undefined.md
            // "A bool value must be false (0) or true (1)."
            var addSpecialTokensByte = unchecked((byte) (addSpecialTokens ? 1 : 0));
            
            if (truncate)
            {
                TokenizerEncodeBatch(tokenizerPtr, textNativeBuffers, outputNativeBuffer, addSpecialTokensByte);
            }
            
            else
            {
                TokenizerEncodeBatchNonTruncating(tokenizerPtr, textNativeBuffers, outputNativeBuffer, addSpecialTokensByte);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch")]
        private static partial void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            NativeBuffer<NativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch_non_truncating")]
        private static partial void TokenizerEncodeBatchNonTruncating(
            nint tokenizerPtr, 
            NativeBuffer<NativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DecodeOutput TokenizerDecode(
            nint tokenizerPtr,
            NativeBuffer<uint> ids,
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_decode")]
        private static partial DecodeOutput TokenizerDecode(nint tokenizerPtr, NativeBuffer<uint> idBuffer);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_decode_skip_special_tokens")]
        private static partial DecodeOutput TokenizerDecodeSkipSpecialTokens(nint tokenizerPtr, NativeBuffer<uint> idBuffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "ids_to_tokens")]
        public static partial nint IDsToTokens(
            nint tokenizerPtr,
            NativeBuffer<uint> idBuffer,
            NativeBuffer<NativeBuffer<byte>> tokenBuffer
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "free_with_handle")]
        public static partial void FreeWithHandle(nint handle);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "free_with_multiple_handles")]
        public static partial void FreeWithMultipleHandles(NativeBuffer<nint> handles);
    }
}