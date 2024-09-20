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

        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern TokenizeOutput TokenizerEncode(nint tokenizerPtr, ReadOnlyNativeBuffer<byte> textNativeBuffer);

        [DllImport(DLL_NAME, EntryPoint = "tokenizer_encode_batch", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TokenizerEncodeBatch(
            nint tokenizerPtr, 
            ReadOnlyNativeBuffer<ReadOnlyNativeBuffer<byte>> textNativeBuffers, 
            NativeBuffer<TokenizeOutput> outputNativeBuffer
        );

        [DllImport(DLL_NAME, EntryPoint = "free_with_handle", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void FreeWithHandle(nint handle);
        
        [DllImport(DLL_NAME, EntryPoint = "free_with_multiple_handles", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void FreeWithMultipleHandles(ReadOnlyNativeBuffer<nint> handles);
    }
}