using Tokenizers.NET;
using Tokenizers.NET.Helpers;

namespace Sample
{
    internal static class Program
    {
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
        
        private static async Task Main(string[] args)
        {
            var tokenizer = (await new TokenizerBuilder()
                .SetExpectedMaxBatches(1024)
                .SetExpectedMaxInputLength(16)
                .SetExceedExpectedMaxBatchesBehavior(ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                .DownloadFromHuggingFaceRepoAsync("TrumpMcDonaldz/OverflowingTokenizer"))
                .ModifyTokenizerConfig((ref TokenizerData x) =>
                {
                    x.Truncation = new()
                    {
                        Direction = "Right",
                        MaxLength = 16,
                        Strategy = "LongestFirst",
                        Stride = 0
                    };

                    x.Truncation = null;
                    x.Padding = null;
                })
                .Build();

            Console.WriteLine($"Truncates: {tokenizer.IsTruncating}\n");
            
            ReadOnlySpan<string> inputTexts = 
            [
                // "Sunset",
                // "I'm fine, thank you!",
                // "I love C#!",
                GenerateString('H', 100),
            ];

            var outputs = tokenizer.TokenizeBatch(inputTexts);
            
            var outputSpan = outputs.Window.AsSpan();

            var index = 0;
            
            foreach (var token in outputSpan)
            {
                const bool TEST_OVERFLOW = true;
                
                if (TEST_OVERFLOW)
                {
                    var overflowIndex = 0;
                    
                    foreach (var overflow in token.OverflowingTokens.AsReadOnlySpan())
                    {
                        const bool SIMPLIFY = false;

                        if (SIMPLIFY)
                        {
                            Console.WriteLine(
                            $"""
                            Overflow IDs {overflowIndex}: {overflow.IDs.AsReadOnlySpan().GetSpanPrintString()}
                            
                            Overflow {overflowIndex} length: {overflow.IDs.Length}

                            """);
                        }

                        else
                        {
                            Console.WriteLine(
                            $"""
                            Overflow IDs {overflowIndex}: {overflow.IDs.AsReadOnlySpan().GetSpanPrintString()}

                            Overflow Attention Mask {overflowIndex}: {overflow.AttentionMask.AsReadOnlySpan().GetSpanPrintString()}

                            Overflow Special Tokens Mask {overflowIndex}: {overflow.SpecialTokensMask.AsReadOnlySpan().GetSpanPrintString()}

                            Overflow Token Type IDs {overflowIndex}: {overflow.TokenTypeIDs.AsReadOnlySpan().GetSpanPrintString()}

                            Overflow {overflowIndex} length: {overflow.IDs.Length}

                            """);
                        }

                        overflowIndex++;
                    }
                }

                var tokenIDs = token.IDs;
                
                Console.WriteLine(
                $"""
                Text: {inputTexts[index++]}

                Input IDs: {tokenIDs.AsReadOnlySpan().GetSpanPrintString()}

                Input IDs Widen: {tokenIDs.Widen().Window.AsSpan().GetSpanPrintString()}

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