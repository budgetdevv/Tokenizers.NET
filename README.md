# Tokenizers.NET

High-performance .NET wrapper for https://github.com/huggingface/tokenizers

# How to install

```
dotnet add package Tokenizers.NET
```

# Why Tokenizers.NET?

- Run HuggingFace tokenizers on .NET
- High performance, zero GC-allocations
- I don't know, TrumpMcDonaldz rock!

# Supported Archs

- OSX ( ARM64 / x86_64 )
- Linux ( ARM64 / x86_64 )
- Windows ( ARM64 / x86_64 )

( Who uses Windows ARM lol )

# View test status

https://budgetdevv.github.io/Tokenizers.NET/


# How to build

- Navigate to project's root directory

```
dotnet clean
dotnet pack -c Release -v:m /p:DownloadNativeFiles=true 
```
