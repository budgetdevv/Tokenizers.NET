use std::alloc::{ dealloc, Layout };
use std::mem::ManuallyDrop;
use std::ptr::null;
use tokenizers::tokenizer::Tokenizer;
use tokenizers::Encoding;

#[repr(C)]
pub struct Buffer<T>
{
    ptr: *mut T,
    length: usize,
}

impl<T> Buffer<T>
{
    pub fn new(ptr: *mut T, length: usize) -> Self
    {
        Buffer
        {
            ptr,
            length,
        }
    }

    pub fn from_slice(slice: &mut [T]) -> Self
    {
        Buffer
        {
            ptr: slice.as_mut_ptr(),
            length: slice.len(),
        }
    }

    pub unsafe fn to_slice(&self) -> &mut [T]
    {
        return std::slice::from_raw_parts_mut(self.ptr, self.length)
    }

    pub fn empty() -> Self
    {
        Buffer
        {
            ptr: std::ptr::null_mut(),
            length: 0,
        }
    }
}

#[repr(C)]
pub struct ReadOnlyBuffer<T>
{
    ptr: *const T,
    length: usize,
}

impl<T> ReadOnlyBuffer<T>
{
    pub fn new(ptr: *const T, length: usize) -> Self
    {
        ReadOnlyBuffer
        {
            ptr,
            length,
        }
    }

    pub fn from_slice(slice: &[T]) -> Self
    {
        ReadOnlyBuffer
        {
            ptr: slice.as_ptr(),
            length: slice.len(),
        }
    }

    pub unsafe fn as_slice(&self) -> &[T]
    {
        std::slice::from_raw_parts(self.ptr, self.length)
    }

    pub fn from_vec(vec: &mut Vec<T>) -> Self
    {
        let ptr = vec.as_mut_ptr();
        let length = vec.len();

        ReadOnlyBuffer
        {
            ptr,
            length,
        }
    }

    pub fn empty() -> Self
    {
        ReadOnlyBuffer
        {
            ptr: std::ptr::null(),
            length: 0,
        }
    }
}

pub struct FreeData
{
    pub ptr: *mut u8,
    pub layout: Layout,
}

impl FreeData
{
    pub unsafe fn from_pointer<T>(ptr: &mut T) -> Self
    {
        let layout = Layout::for_value(&*ptr);

        return FreeData
        {
            ptr: (ptr as *mut T) as *mut u8,
            layout,
        }
    }

    pub unsafe fn from_pointer_and_box<T>(ptr: &mut T) -> *mut Self
    {
        let layout = Layout::for_value(ptr);

        let result = Box::new(FreeData
        {
            ptr: (ptr as *mut T) as *mut u8,
            layout,
        });

        return Box::into_raw(result);
    }
}

#[repr(C)]
pub struct TokenizeOutput
{
    pub ids: ReadOnlyBuffer<u32>,
    pub attention_mask: ReadOnlyBuffer<u32>,
    pub special_tokens_mask: ReadOnlyBuffer<u32>,
    pub overflowing_tokens: ReadOnlyBuffer<TokenizeOutputOverflowedToken>,
    pub original_output_free_handle: *const FreeData,
    pub overflowing_tokens_free_handle: *const FreeData,
}

impl TokenizeOutput
{
    #[inline(always)]
    pub unsafe fn from_encoded_tokens(encoded_tokens: Encoding, truncate: bool) -> Self
    {
        let encoded_tokens = Box::new(encoded_tokens);

        let ids = ReadOnlyBuffer::from_slice(encoded_tokens.get_ids());
        let attention_mask = ReadOnlyBuffer::from_slice(encoded_tokens.get_attention_mask());
        let special_tokens_mask = ReadOnlyBuffer::from_slice(encoded_tokens.get_special_tokens_mask());

        let overflowing_tokens_slice = encoded_tokens.get_overflowing();

        let overflowing_tokens: ReadOnlyBuffer<TokenizeOutputOverflowedToken>;

        let overflowing_tokens_free_handle: *const FreeData;

        if (truncate && overflowing_tokens_slice.len() > 0)
        {
            let mut overflowing_tokens_vec = overflowing_tokens_slice
                .iter()
                .map(|overflowing_token|
                    TokenizeOutputOverflowedToken::from_overflowing_encoded_tokens(overflowing_token))
                .collect::<Vec<TokenizeOutputOverflowedToken>>();

            // println!("Overflowing tokens: {:?}", overflowing_tokens.as_slice().len());

            overflowing_tokens_free_handle = FreeData::from_pointer_and_box(
                &mut *(overflowing_tokens_vec.as_mut_ptr())
            );

            overflowing_tokens = ReadOnlyBuffer::from_vec(&mut ManuallyDrop::new(overflowing_tokens_vec));
        }

        else
        {
            overflowing_tokens = ReadOnlyBuffer::empty();
            overflowing_tokens_free_handle = null();
        }

        // into_raw() keeps encoded_tokens alive
        let encoded_tokens_ptr = Box::into_raw(encoded_tokens);

        let original_output_free_handle = FreeData::from_pointer_and_box(
            &mut *encoded_tokens_ptr
        );

        return TokenizeOutput
        {
            ids,
            attention_mask,
            special_tokens_mask,
            overflowing_tokens,
            original_output_free_handle,
            overflowing_tokens_free_handle,
        };
    }
}

#[repr(C)]
pub struct TokenizeOutputOverflowedToken
{
    pub ids: ReadOnlyBuffer<u32>,
    pub attention_mask: ReadOnlyBuffer<u32>,
    pub special_tokens_mask: ReadOnlyBuffer<u32>,
}

impl TokenizeOutputOverflowedToken
{
    #[inline(always)]
    pub unsafe fn from_overflowing_encoded_tokens(encoded_tokens: &Encoding) -> Self
    {
        let ids = ReadOnlyBuffer::from_slice(encoded_tokens.get_ids());
        let attention_mask = ReadOnlyBuffer::from_slice(encoded_tokens.get_attention_mask());
        let special_tokens_mask = ReadOnlyBuffer::from_slice(encoded_tokens.get_special_tokens_mask());

        return TokenizeOutputOverflowedToken
        {
            ids,
            attention_mask,
            special_tokens_mask,
        };
    }
}

#[no_mangle]
pub unsafe extern "C" fn allocate_tokenizer(
    json_bytes_ptr: *const u8,
    json_bytes_length: usize,
) -> *const Tokenizer
{
    let json_bytes = std::slice::from_raw_parts(json_bytes_ptr, json_bytes_length);

    let tokenizer = Tokenizer::from_bytes(json_bytes).unwrap();

    // Allocate on heap and return raw pointer
    return Box::into_raw(Box::new(tokenizer));
}

#[no_mangle]
pub unsafe extern "C" fn free_tokenizer(tokenizer_handle: *mut Tokenizer)
{
    // Drop the tokenizer
    drop(Box::from_raw(tokenizer_handle));
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_encode(
    tokenizer_ptr: *mut Tokenizer,
    text_buffer: ReadOnlyBuffer<u8>) -> TokenizeOutput
{
    return tokenizer_encode_core(tokenizer_ptr, text_buffer, true);
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_encode_non_truncating(
    tokenizer_ptr: *mut Tokenizer,
    text_buffer: ReadOnlyBuffer<u8>) -> TokenizeOutput
{
    return tokenizer_encode_core(tokenizer_ptr, text_buffer, false);
}

#[inline(always)]
pub unsafe extern "C" fn tokenizer_encode_core(
    tokenizer_ptr: *mut Tokenizer,
    text_buffer: ReadOnlyBuffer<u8>,
    truncate: bool)
    -> TokenizeOutput
{
    let tokenizer = &*tokenizer_ptr;

    let text = std::str::from_utf8_unchecked(text_buffer.as_slice());

    let encoded_result = tokenizer.encode(text, true);

    let encoded_tokens = match encoded_result
    {
        Ok(encoded) => encoded,
        Err(err) => panic!("{}", err),
    };

    return TokenizeOutput::from_encoded_tokens(encoded_tokens, truncate);
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_encode_batch(
    tokenizer_ptr: *mut Tokenizer,
    text_buffers: ReadOnlyBuffer<ReadOnlyBuffer<u8>>,
    output_buffer: Buffer<TokenizeOutput>)
{
    tokenizer_encode_batch_core(tokenizer_ptr, text_buffers, output_buffer, true);
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_encode_batch_non_truncating(
    tokenizer_ptr: *mut Tokenizer,
    text_buffers: ReadOnlyBuffer<ReadOnlyBuffer<u8>>,
    output_buffer: Buffer<TokenizeOutput>)
{
    tokenizer_encode_batch_core(tokenizer_ptr, text_buffers, output_buffer, false);
}

#[inline(always)]
pub unsafe extern "C" fn tokenizer_encode_batch_core(
    tokenizer_ptr: *mut Tokenizer,
    text_buffers: ReadOnlyBuffer<ReadOnlyBuffer<u8>>,
    output_buffer: Buffer<TokenizeOutput>,
    truncate: bool)
{
    let tokenizer = &*tokenizer_ptr;

    let texts = text_buffers
        .as_slice()
        .iter()
        .map(|text_buffer| std::str::from_utf8_unchecked(text_buffer.as_slice()))
        .collect::<Vec<&str>>();

    let encoded_result = tokenizer.encode_batch(texts, true);

    let encoded_tokens = match encoded_result
    {
        Ok(encoded) => encoded,
        Err(err) => panic!("{}", err),
    };

    let mut current_ptr = output_buffer.ptr;

    // println!("{:?}", current_ptr);

    for encoded_token in encoded_tokens
    {
        *current_ptr = TokenizeOutput::from_encoded_tokens(encoded_token, truncate);

        current_ptr = current_ptr.add(1);
    }
}

#[no_mangle]
pub unsafe extern "C" fn free_with_handle(handle: *mut FreeData)
{
    let free_data = Box::from_raw(handle);

    // println!("Freeing memory at {:p}", free_data.ptr);
    // println!("With layout {:?}", free_data.layout);

    dealloc(free_data.ptr, free_data.layout);
}

#[no_mangle]
pub unsafe extern "C" fn free_with_multiple_handles(handle: ReadOnlyBuffer<*mut FreeData>)
{
    for free_data in handle.as_slice()
    {
        let free_data = Box::from_raw(*free_data);

        // println!("Freeing memory at {:p}", free_data.ptr);
        // println!("With layout {:?}", free_data.layout);

        dealloc(free_data.ptr, free_data.layout);
    }
}
