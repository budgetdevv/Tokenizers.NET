use std::string::String;
use std::marker::PhantomData;
use std::ptr::{ null, null_mut };
use std::slice;
use tokenizers::tokenizer::Tokenizer;
use tokenizers::Encoding;
// #[inline(always)] is used aggressively - Realistically we only have a few callsites.

#[repr(C)]
union RawPointer<T>
{
    mutable: *mut T,
    readonly: *const T,
}

#[repr(C)]
pub struct NativeBuffer<T>
{
    pub ptr: RawPointer<T>,
    pub length: usize,
}

impl<T> NativeBuffer<T>
{
    #[inline(always)]
    pub fn wrap_mutable_ptr(ptr: *mut T, length: usize) -> Self
    {
        NativeBuffer
        {
            ptr: RawPointer { mutable: ptr },
            length,
        }
    }

    #[inline(always)]
    pub fn wrap_ptr(ptr: *const T, length: usize) -> Self
    {
        NativeBuffer
        {
            ptr: RawPointer { readonly: ptr },
            length,
        }
    }

    #[inline(always)]
    pub fn from_slice(slice: &[T]) -> Self
    {
        NativeBuffer
        {
            ptr: RawPointer { readonly: slice.as_ptr() },
            length: slice.len(),
        }
    }

    #[inline(always)]
    pub fn from_mutable_slice(slice: &mut [T]) -> Self
    {
        NativeBuffer
        {
            ptr: RawPointer { mutable: slice.as_mut_ptr() },
            length: slice.len(),
        }
    }

    #[inline(always)]
    pub unsafe fn as_slice(&self) -> &[T]
    {
        return slice::from_raw_parts(self.ptr.readonly, self.length)
    }

    #[inline(always)]
    pub unsafe fn as_mutable_slice(&self) -> &mut [T]
    {
        return slice::from_raw_parts_mut(self.ptr.mutable, self.length)
    }

    #[inline(always)]
    pub fn from_vec(vec: &Vec<T>) -> Self
    {
        let ptr = vec.as_ptr();
        let length = vec.len();

        return NativeBuffer
        {
            ptr: RawPointer { readonly: ptr },
            length,
        }
    }

    #[inline(always)]
    pub fn from_mutable_vec(vec: &mut Vec<T>) -> Self
    {
        let ptr = vec.as_mut_ptr();
        let length = vec.len();

        return NativeBuffer
        {
            ptr: RawPointer { mutable: ptr },
            length,
        }
    }

    #[inline(always)]
    pub fn empty() -> Self
    {
        NativeBuffer
        {
            ptr: RawPointer { mutable: null_mut() },
            length: 0,
        }
    }
}

pub struct DropHandle<T=()>
{
    pub ptr_to_box: *mut (),
    pub drop_callback: fn(*mut ()),
    stop_complaining_you_bitch: PhantomData<T>,
}

impl <T> DropHandle<T>
{
    #[inline(always)]
    pub unsafe fn from_value_and_allocate_box(value: T) -> *mut DropHandle<T>
    {
        let val_box = Box::new(value);

        let drop_callback = |ptr: *mut()|
        {
            let actual_ptr = ptr as *mut T;
            let _ = Box::from_raw(actual_ptr);
        };

        let handle = Box::new(DropHandle
        {
            // into_raw() keeps the box alive
            ptr_to_box: Box::into_raw(val_box) as *mut (),
            drop_callback,
            stop_complaining_you_bitch: Default::default(),
        });

        // into_raw() keeps the box alive
        return Box::into_raw(handle);
    }

    #[inline(always)]
    pub unsafe fn from_handle(handle: *mut DropHandle<T>) -> Box<DropHandle<T>>
    {
        return Box::from_raw(handle);
    }
}

#[repr(C)]
pub struct TokenizeOutput
{
    pub ids: NativeBuffer<u32>,
    pub attention_mask: NativeBuffer<u32>,
    pub special_tokens_mask: NativeBuffer<u32>,
    pub token_type_ids: NativeBuffer<u32>,
    pub overflowing_tokens: NativeBuffer<TokenizeOutputOverflowedToken>,
    pub original_output_free_handle: *const DropHandle<Encoding>,
    pub overflowing_tokens_free_handle: *const DropHandle<Vec<TokenizeOutputOverflowedToken>>,
}

impl TokenizeOutput
{
    #[inline(always)]
    pub unsafe fn from_encoded_tokens(encoded_tokens: Encoding, truncate: bool) -> Self
    {
        // println!("Offsets {:?}", encoded_tokens.get_offsets());

        let ids = NativeBuffer::from_slice(encoded_tokens.get_ids());
        let attention_mask = NativeBuffer::from_slice(encoded_tokens.get_attention_mask());
        let special_tokens_mask = NativeBuffer::from_slice(encoded_tokens.get_special_tokens_mask());
        let token_type_ids = NativeBuffer::from_slice(encoded_tokens.get_type_ids());

        let overflowing_tokens_slice = encoded_tokens.get_overflowing();

        let overflowing_tokens: NativeBuffer<TokenizeOutputOverflowedToken>;

        let overflowing_tokens_free_handle: *const DropHandle<Vec<TokenizeOutputOverflowedToken>>;

        if truncate && overflowing_tokens_slice.len() > 0
        {
            let mut overflowing_tokens_vec = overflowing_tokens_slice
                .iter()
                .map(|overflowing_token|
                    TokenizeOutputOverflowedToken::from_overflowing_encoded_tokens(overflowing_token))
                .collect::<Vec<TokenizeOutputOverflowedToken>>();

            // println!("Overflowing tokens: {:?}", overflowing_tokens.as_slice().len());

            overflowing_tokens = NativeBuffer::from_mutable_vec(&mut overflowing_tokens_vec);

            overflowing_tokens_free_handle = DropHandle::from_value_and_allocate_box(
                overflowing_tokens_vec
            );
        }

        else
        {
            overflowing_tokens = NativeBuffer::empty();
            overflowing_tokens_free_handle = null();
        }

        let original_output_free_handle =
            DropHandle::from_value_and_allocate_box(encoded_tokens);

        return TokenizeOutput
        {
            ids,
            attention_mask,
            special_tokens_mask,
            token_type_ids,
            overflowing_tokens,
            original_output_free_handle,
            overflowing_tokens_free_handle,
        };
    }
}

#[repr(C)]
pub struct TokenizeOutputOverflowedToken
{
    pub ids: NativeBuffer<u32>,
    pub attention_mask: NativeBuffer<u32>,
    pub special_tokens_mask: NativeBuffer<u32>,
    pub token_type_ids: NativeBuffer<u32>,
}

impl TokenizeOutputOverflowedToken
{
    #[inline(always)]
    pub unsafe fn from_overflowing_encoded_tokens(encoded_tokens: &Encoding) -> Self
    {
        let ids = NativeBuffer::from_slice(encoded_tokens.get_ids());
        let attention_mask = NativeBuffer::from_slice(encoded_tokens.get_attention_mask());
        let special_tokens_mask = NativeBuffer::from_slice(encoded_tokens.get_special_tokens_mask());
        let token_type_ids = NativeBuffer::from_slice(encoded_tokens.get_type_ids());

        return TokenizeOutputOverflowedToken
        {
            ids,
            attention_mask,
            special_tokens_mask,
            token_type_ids,
        };
    }
}

#[no_mangle]
pub unsafe extern "C" fn allocate_tokenizer(json_bytes: NativeBuffer<u8>) -> *mut Tokenizer
{
    let json_bytes = json_bytes.as_slice();

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
    text_buffer: NativeBuffer<u8>,
    add_special_tokens: bool)
    -> TokenizeOutput
{
    return tokenizer_encode_core(
        tokenizer_ptr,
        text_buffer,
        true,
        add_special_tokens
    );
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_encode_non_truncating(
    tokenizer_ptr: *mut Tokenizer,
    text_buffer: NativeBuffer<u8>,
    add_special_tokens: bool)
    -> TokenizeOutput
{
    return tokenizer_encode_core(
        tokenizer_ptr,
        text_buffer,
        false,
        add_special_tokens
    );
}

#[inline(always)]
pub unsafe extern "C" fn tokenizer_encode_core(
    tokenizer_ptr: *mut Tokenizer,
    text_buffer: NativeBuffer<u8>,
    truncate: bool,
    add_special_tokens: bool)
    -> TokenizeOutput
{
    let tokenizer = &*tokenizer_ptr;

    let text = std::str::from_utf8_unchecked(text_buffer.as_slice());

    let encoded_result = tokenizer.encode_fast(text, add_special_tokens);

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
    text_buffers: NativeBuffer<NativeBuffer<u8>>,
    output_buffer: NativeBuffer<TokenizeOutput>,
    add_special_tokens: bool)
{
    tokenizer_encode_batch_core(
        tokenizer_ptr,
        text_buffers,
        output_buffer,
        true,
        add_special_tokens
    );
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_encode_batch_non_truncating(
    tokenizer_ptr: *mut Tokenizer,
    text_buffers: NativeBuffer<NativeBuffer<u8>>,
    output_buffer: NativeBuffer<TokenizeOutput>,
    add_special_tokens: bool)
{
    tokenizer_encode_batch_core(
        tokenizer_ptr,
        text_buffers,
        output_buffer,
        false,
        add_special_tokens
    );
}

#[inline(always)]
pub unsafe extern "C" fn tokenizer_encode_batch_core(
    tokenizer_ptr: *mut Tokenizer,
    text_buffers: NativeBuffer<NativeBuffer<u8>>,
    output_buffer: NativeBuffer<TokenizeOutput>,
    truncate: bool,
    add_special_tokens: bool)
{
    let tokenizer = &*tokenizer_ptr;

    let texts = text_buffers
        .as_slice()
        .iter()
        .map(|text_buffer| std::str::from_utf8_unchecked(text_buffer.as_slice()))
        .collect::<Vec<&str>>();

    let encoded_result = tokenizer.encode_batch_fast(texts, add_special_tokens);

    let encoded_tokens = match encoded_result
    {
        Ok(encoded) => encoded,
        Err(error) => panic!("{}", error),
    };

    let mut current_ptr = output_buffer.ptr.mutable;

    for encoded_token in encoded_tokens
    {
        *current_ptr = TokenizeOutput::from_encoded_tokens(encoded_token, truncate);

        current_ptr = current_ptr.add(1);
    }
}

#[repr(C)]
pub struct DecodeOutput
{
    pub text_buffer: NativeBuffer<u8>,
    pub free_handle: *mut DropHandle<String>
}

impl DecodeOutput
{
    #[inline(always)]
    pub unsafe fn from_text(mut text: String) -> Self
    {
        let text_bytes = text.as_mut_vec();

        let text_buffer = NativeBuffer::from_mutable_vec(text_bytes);

        let free_handle = DropHandle::from_value_and_allocate_box(text);

        return DecodeOutput
        {
            text_buffer,
            free_handle,
        };
    }
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_decode(
    tokenizer_ptr: *mut Tokenizer,
    id_buffer: NativeBuffer<u32>)
    -> DecodeOutput
{
    return tokenizer_decode_core(tokenizer_ptr, id_buffer, false);
}

#[no_mangle]
pub unsafe extern "C" fn tokenizer_decode_skip_special_tokens(
    tokenizer_ptr: *mut Tokenizer,
    id_buffer: NativeBuffer<u32>)
    -> DecodeOutput
{
    return tokenizer_decode_core(tokenizer_ptr, id_buffer, true);
}

#[inline(always)]
pub unsafe extern "C" fn tokenizer_decode_core(
    tokenizer_ptr: *mut Tokenizer,
    id_buffer: NativeBuffer<u32>,
    skip_special_tokens: bool)
    -> DecodeOutput
{
    let tokenizer = &*tokenizer_ptr;

    let text = tokenizer.decode(id_buffer.as_slice(), skip_special_tokens).unwrap();

    return DecodeOutput::from_text(text);
}

#[no_mangle]
#[inline(always)]
pub unsafe extern "C" fn ids_to_tokens(
    tokenizer_ptr: *mut Tokenizer,
    id_buffer: NativeBuffer<u32>,
    token_buffer: NativeBuffer<NativeBuffer<u8>>)
    -> *mut DropHandle<Vec<String>>
{
    let tokenizer = &*tokenizer_ptr;

    let mut token_buffers = Vec::with_capacity(id_buffer.length);

    let mut current_token_ptr = token_buffer.ptr.mutable;

    for id in id_buffer.as_slice()
    {
        let mut token = tokenizer.id_to_token(*id).unwrap();

        *current_token_ptr = NativeBuffer::from_mutable_vec(token.as_mut_vec());

        current_token_ptr = current_token_ptr.add(1);

        token_buffers.push(token);
    }

    return DropHandle::from_value_and_allocate_box(token_buffers);
}

#[no_mangle]
#[inline(always)]
pub unsafe extern "C" fn free_with_handle(handle: *mut DropHandle<()>)
{
    let free_data = DropHandle::from_handle(handle);

    // println!("Freeing memory at {:p}", free_data.ptr_to_box);

    let drop_callback = free_data.drop_callback;

    drop_callback(free_data.ptr_to_box);
}

#[no_mangle]
pub unsafe extern "C" fn free_with_multiple_handles(handle: NativeBuffer<*mut DropHandle<()>>)
{
    for free_data in handle.as_slice()
    {
        free_with_handle(*free_data);
    }
}
