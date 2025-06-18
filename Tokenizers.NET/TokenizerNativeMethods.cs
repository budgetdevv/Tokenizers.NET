using System.Runtime.InteropServices;
using NativeMemory;
using Tokenizers.NET.Outputs;

namespace Tokenizers.NET
{
    internal static unsafe partial class TokenizerNativeMethods
    {
        private const string DLL_NAME = "tokenizers_net";

        [LibraryImport(DLL_NAME, EntryPoint = "allocate_tokenizer")]
        public static partial nint AllocateTokenizer(MemoryWindow<byte> jsonBytes);
        
        [LibraryImport(DLL_NAME, EntryPoint = "free_tokenizer")]
        public static partial void FreeTokenizer(nint tokenizerHandle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            MemoryWindow<byte> textNativeBuffer,
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
            MemoryWindow<byte> textNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_non_truncating")]
        private static partial TokenizeOutput TokenizerEncodeNonTruncating(
            nint tokenizerPtr,
            MemoryWindow<byte> textNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            MemoryWindow<MemoryWindow<byte>> textNativeBuffers,
            MemoryWindow<TokenizeOutput> outputNativeBuffer,
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
            MemoryWindow<MemoryWindow<byte>> textNativeBuffers,
            MemoryWindow<TokenizeOutput> outputNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch_non_truncating")]
        private static partial void TokenizerEncodeBatchNonTruncating(
            nint tokenizerPtr, 
            MemoryWindow<MemoryWindow<byte>> textNativeBuffers,
            MemoryWindow<TokenizeOutput> outputNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DecodeOutput TokenizerDecode(
            nint tokenizerPtr,
            MemoryWindow<uint> ids,
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
        private static partial DecodeOutput TokenizerDecode(nint tokenizerPtr, MemoryWindow<uint> idBuffer);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_decode_skip_special_tokens")]
        private static partial DecodeOutput TokenizerDecodeSkipSpecialTokens(nint tokenizerPtr, MemoryWindow<uint> idBuffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "ids_to_tokens")]
        public static partial nint IDsToTokens(
            nint tokenizerPtr,
            MemoryWindow<uint> idBuffer,
            MemoryWindow<MemoryWindow<byte>> tokenBuffer
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "free_with_handle")]
        public static partial void FreeWithHandle(nint handle);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport(DLL_NAME, EntryPoint = "free_with_multiple_handles")]
        public static partial void FreeWithMultipleHandles(MemoryWindow<nint> handles);
    }
}