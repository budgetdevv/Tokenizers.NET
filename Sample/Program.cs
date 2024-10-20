﻿using Tokenizers.NET;

namespace Sample
{
    internal static class Program
    {
        private struct Config: Tokenizer.IConfig
        {
            public static Tokenizer.ConfigBuilder ConfigBuilder=> 
                new Tokenizer.ConfigBuilder()
                    .SetExpectedMaxBatches(1024)
                    .SetExpectedMaxInputLength(16)
                    .SetExceedExpectedMaxBatchesBehavior(Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                    .SetTokenizerJsonPath("OverflowingTokenizer.json");
        }

        private static string GenerateString(char val, int length)
        {
            return string.Create(length, length, (buffer, len) =>
            {
                for (var i = 0; i < len; i++)
                {
                    buffer[i] = val;
                }
            });
        }
        
        private static void Main(string[] args)
        {
            var tokenizer = new Tokenizer<Config>();

            Console.WriteLine($"Truncates: {tokenizer.Truncate}\n");
            
            ReadOnlySpan<string> inputTexts = 
            [
                // "Sunset",
                // "I'm fine, thank you!",
                // "I love C#!",
                GenerateString('H', 5000),
            ];

            var outputs = tokenizer.TokenizeBatch(inputTexts);
            
            var outputSpan = outputs.Buffer.AsSpan();

            var index = 0;
            
            foreach (var token in outputSpan)
            {
                const bool TEST_OVERFLOW = false;
                
                if (TEST_OVERFLOW)
                {
                    var overflowIndex = 0;
                    
                    foreach (var overflow in token.OverflowingTokens.AsReadOnlySpan())
                    {
                        Console.WriteLine(
                        $"""
                        Overflow IDs {overflowIndex}: {overflow.IDs.AsReadOnlySpan().GetSpanPrintString()}
                        
                        Overflow Attention Mask {overflowIndex}: {overflow.AttentionMask.AsReadOnlySpan().GetSpanPrintString()}
                        
                        Overflow Special Tokens Mask {overflowIndex}: {overflow.SpecialTokensMask.AsReadOnlySpan().GetSpanPrintString()}
                        
                        Overflow Token Type IDs {overflowIndex}: {overflow.TokenTypeIDs.AsReadOnlySpan().GetSpanPrintString()}
                        
                        Overflow {overflowIndex} length: {overflow.IDs.Length}
                        
                        """);
                        
                        overflowIndex++;
                    }
                }

                var tokenIDs = token.IDs;
                
                Console.WriteLine(
                $"""
                Text: {inputTexts[index++]}
                
                Input IDs: {tokenIDs.AsReadOnlySpan().GetSpanPrintString()}
                
                Input IDs Widen: {tokenIDs.Widen().Buffer.AsSpan().GetSpanPrintString()}
                
                Attention Mask: {token.AttentionMask.AsReadOnlySpan().GetSpanPrintString()}
                
                Special Tokens Mask: {token.SpecialTokensMask.AsReadOnlySpan().GetSpanPrintString()}
                
                Token Type IDs: {token.TokenTypeIDs.AsReadOnlySpan().GetSpanPrintString()}
                
                Input IDs Length: {token.IDs.Length}
                
                """);

                const bool TEST_DECODE = false;
                
                if (TEST_DECODE)
                {
                    var decodedTextResult = tokenizer.Decode(token.IDs, true);
                
                    var decodedText = decodedTextResult.ToString();

                    Console.WriteLine(decodedText);
                
                    decodedTextResult.Dispose();
                }
                
                token.Dispose();
            }
            
            outputs.Dispose();

            const bool TEST_SINGLE_TOKENIZE = false;
            
            if (TEST_SINGLE_TOKENIZE)
            {
                using var output = tokenizer.Tokenize("Hi");
            
                // Console.WriteLine(output.IDs.AsReadOnlySpan().GetSpanPrintString());
                Console.WriteLine($"Overflowing Tokens Length: {output.OverflowingTokens.Length}\n");
            }
            
            Console.WriteLine("Done!");
            
            tokenizer.Dispose();
        }
    }
}