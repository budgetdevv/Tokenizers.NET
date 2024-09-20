# Tokenizers.NET

High-performance .NET wrapper for https://github.com/huggingface/tokenizers

# How to install

```
dotnet add package Tokenizers.NET --version 1.0.0
```

# Why Tokenizers.NET?

- Run HuggingFace tokenizers on .NET
- High performance, zero GC-allocations
- I don't know, TrumpMcDonaldz rock!

# Supported Archs

- OSX ( ARM64 / x86_64 )
- Linux ( ARM64 / x86_64 )
- Windows ( x86_64 )

( Who uses Windows ARM lol )

# How to build native libs ( Only tested on Apple M2, should in theory work on devices running MacOS )

- Install Docker ( https://docs.docker.com/engine/install/ )

```
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh ( Install Rust )
rustup target add aarch64-apple-darwin
rustup target add x86_64-apple-darwin
cargo install cross --git https://github.com/cross-rs/cross
brew install mingw-w64
```

- Find the line that says `<Target Name="BuildNative" BeforeTargets="Build" Condition="False">` in `Tokenizers.NET.csproj` and set Condition="True"
- Build the project
