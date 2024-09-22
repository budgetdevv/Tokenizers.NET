using System.Text;
using Tokenizers.NET;

namespace Sample
{
    internal static class Program
    {
        private struct Config: ITokenizerConfig
        {
            public static uint ExpectedMaxInputLength => 1024;
            
            public static uint ExpectedMaxBatches => 5;
            
            
            public static string TokenizerJsonPath => "FlorenceTokenizer.json";
        }

        private static string GenerateString(char val, int length)
        {
            return string.Create(length, 
                length, 
                (buffer, len) =>
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

            Console.WriteLine($"Truncates: {tokenizer.Truncate}");
            
            ReadOnlySpan<string> inputTexts = 
            [
                "Sunset",
                "I'm fine, thank you!",
                "I love C#!",
                // GenerateString('H', 4096),
            ];

            var outputs = tokenizer.Tokenize(inputTexts);
            
            var outputSpan = outputs.Memory.AsSpan();

            var index = 0;
            
            foreach (var token in outputSpan)
            {
                foreach (var overflow in token.OverflowingTokens.AsReadOnlySpan())
                {
                    // Console.WriteLine($"Overflow: {overflow.IDs.AsReadOnlySpan().GetSpanPrintString()}\n\n");

                    Console.WriteLine($"Overflow Length: {overflow.IDs.Length}");
                }
                
                Console.WriteLine(
                $"""
                Text: {inputTexts[index++]}
                Input IDs: {token.IDs.AsReadOnlySpan().GetSpanPrintString()}
                Attention Mask: {token.AttentionMask.AsReadOnlySpan().GetSpanPrintString()}
                """);

                Console.WriteLine($"Input IDs Length: {token.IDs.Length}");

                var decodedTextResult = tokenizer.Decode(token.IDs, true);
                
                var decodedText = Encoding.UTF8.GetString(decodedTextResult.TextBuffer.AsReadOnlySpan());

                Console.WriteLine(decodedText);
                
                decodedTextResult.Dispose();
                
                token.Dispose();
            }
            
            outputs.Dispose();

            const bool TEST_SINGLE_TOKENIZE = false;
            
            if (TEST_SINGLE_TOKENIZE)
            {
                using var output = tokenizer.Tokenize("Hi");
            
                // Console.WriteLine(output.IDs.AsReadOnlySpan().GetSpanPrintString());
                Console.WriteLine($"Overflowing Tokens Length: {output.OverflowingTokens.Length}");
            }
            
            Console.WriteLine("Done!");
            
            tokenizer.Dispose();
        }
    }
}