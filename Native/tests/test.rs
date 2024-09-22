// use tokenizers_net::{allocate_tokenizer, free_tokenizer, tokenizer_decode, tokenizer_encode, ReadOnlyBuffer};
//
// #[test]
// fn test_tokenizer_encode() {
//     let json_str = r#"{
//             "type": "wordpiece",
//             "unk_token": "[UNK]",
//             "vocab": {"hello": 0, "world": 1, "[UNK]": 2}
//         }"#;
//     let json_bytes = json_str.as_bytes();
//
//     unsafe {
//         // Allocate the tokenizer
//         let tokenizer_ptr = allocate_tokenizer(json_bytes.as_ptr(), json_bytes.len());
//         assert!(!tokenizer_ptr.is_null());
//
//         // Create a buffer with input text
//         let text = "hello world";
//         let text_buffer = ReadOnlyBuffer::from_slice(text.as_bytes());
//
//         // Encode the text
//         let output = tokenizer_encode(tokenizer_ptr, text_buffer);
//
//         // Check the output
//         assert_eq!(output.ids.length, 2);  // Should have two tokens "hello" and "world"
//         let ids = output.ids.as_slice();
//         assert_eq!(ids, &[0, 1]);
//
//         // Free the tokenizer
//         tokenizers_net::free_tokenizer(tokenizer_ptr);
//     }
// }
//
// #[test]
// fn test_tokenizer_decode() {
//     let json_str = r#"{
//             "type": "wordpiece",
//             "unk_token": "[UNK]",
//             "vocab": {"hello": 0, "world": 1, "[UNK]": 2}
//         }"#;
//     let json_bytes = json_str.as_bytes();
//
//     unsafe {
//         // Allocate the tokenizer
//         let tokenizer_ptr = allocate_tokenizer(json_bytes.as_ptr(), json_bytes.len());
//         assert!(!tokenizer_ptr.is_null());
//
//         // Create a buffer with token ids
//         let ids = vec![0, 1];  // "hello world"
//         let id_buffer = ReadOnlyBuffer::from_slice(&ids);
//
//         // Decode the ids
//         let decoded_output = tokenizer_decode(tokenizer_ptr, id_buffer);
//
//         // Check the output
//         let decoded_text = std::str::from_utf8(decoded_output.text_buffer.as_slice()).unwrap();
//         assert_eq!(decoded_text, "hello world");
//
//         // Free the tokenizer
//         free_tokenizer(tokenizer_ptr);
//     }
// }
