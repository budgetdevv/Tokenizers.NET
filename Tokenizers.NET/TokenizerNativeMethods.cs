using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    internal static unsafe partial class TokenizerNativeMethods
    {
        private const string DLL_NAME = "tokenizers_net";

        [LibraryImport(DLL_NAME, EntryPoint = "allocate_tokenizer")]
        public static partial nint AllocateTokenizer(ReadOnlyNativeBuffer<byte> jsonBytes);
        
        [LibraryImport(DLL_NAME, EntryPoint = "free_tokenizer")]
        public static partial void FreeTokenizer(nint tokenizerHandle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<byte> textNativeBuffer,
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
        
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static partial TokenizeOutput TokenizerEncode(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<byte> textNativeBuffer,
            byte addSpecialTokens
        );
        
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_non_truncating")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static partial TokenizeOutput TokenizerEncodeNonTruncating(
            nint tokenizerPtr,
            ReadOnlyNativeBuffer<byte> textNativeBuffer,
            byte addSpecialTokens
        );
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
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
        
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static partial void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            byte addSpecialTokens
        );
        
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch_non_truncating")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static partial void TokenizerEncodeBatchNonTruncating(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer,
            byte addSpecialTokens
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
        
        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_decode")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static partial DecodeOutput TokenizerDecode(nint tokenizerPtr, ReadOnlyNativeBuffer<uint> idBuffer);

        [LibraryImport(DLL_NAME, EntryPoint = "tokenizer_decode_skip_special_tokens")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static partial DecodeOutput TokenizerDecodeSkipSpecialTokens(nint tokenizerPtr, ReadOnlyNativeBuffer<uint> idBuffer);

        [LibraryImport(DLL_NAME, EntryPoint = "free_with_handle")]
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static partial void FreeWithHandle(nint handle);
        
        [LibraryImport(DLL_NAME, EntryPoint = "free_with_multiple_handles")] 
        [SuppressGCTransition, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static partial void FreeWithMultipleHandles(ReadOnlyNativeBuffer<nint> handles);
    }
}