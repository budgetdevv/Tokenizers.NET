// <auto-generated>
// This code is generated by csbindgen.
// DON'T CHANGE THIS DIRECTLY.
// </auto-generated>
#pragma warning disable CS8500
#pragma warning disable CS8981
using System;
using System.Runtime.InteropServices;


namespace CsBindgen
{
    internal static unsafe partial class NativeMethods
    {
        const string __DllName = "TokenizersNETNative";



        [DllImport(__DllName, EntryPoint = "allocate_tokenizer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern Tokenizer* allocate_tokenizer(byte* json_bytes_ptr, nuint json_bytes_length);

        [DllImport(__DllName, EntryPoint = "free_tokenizer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void free_tokenizer(Tokenizer* tokenizer_handle);

        [DllImport(__DllName, EntryPoint = "tokenizer_encode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern TokenizeOutput tokenizer_encode(Tokenizer* tokenizer_ptr, ReadOnlyBuffer text_buffer);

        [DllImport(__DllName, EntryPoint = "tokenizer_encode_batch", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void tokenizer_encode_batch(Tokenizer* tokenizer_ptr, ReadOnlyBuffer text_buffers, Buffer output_buffer);

        [DllImport(__DllName, EntryPoint = "free_with_handle", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void free_with_handle(FreeData* handle);


    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct Buffer
    {
        public T* ptr;
        public nuint length;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct ReadOnlyBuffer
    {
        public T* ptr;
        public nuint length;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct FreeData
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct TokenizeOutput
    {
        public ReadOnlyBuffer ids;
        public ReadOnlyBuffer attention_mask;
        public ReadOnlyBuffer special_tokens_mask;
        public ReadOnlyBuffer overflowing_tokens;
        public FreeData* original_output_free_handle;
    }



}
