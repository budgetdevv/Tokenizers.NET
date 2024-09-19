fn main()
{
    let result = csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("TokenizersNETNative")
        .csharp_class_name("NativeMethods")
        .generate_csharp_file("./GeneratedCSharp/NativeMethods.cs");

    match result {
        Ok(_) => println!("C# bindings generated successfully"),
        Err(e) => eprintln!("Failed to generate C# bindings: {}", e),
    }
}