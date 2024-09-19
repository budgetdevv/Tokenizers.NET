using Tokenizers.NET;

namespace Sample
{
    internal static class Program
    {
        private struct Config: ITokenizerConfig
        {
            public static uint ExpectedMaxInputLength => 1024;
            
            public static uint ExpectedMaxBatches => 5;
            
            public static string TokenizerJsonPath => "tokenizer.json";
        }
        
        private static void Main(string[] args)
        {
            var tokenizer = new Tokenizer<Config>();
            
            ReadOnlySpan<string> inputTexts = 
            [
                "Sunset",
                "I'm fine, thank you!",
                "I love C#!"
            ];

            var output = tokenizer.Tokenize(inputTexts);
            
            var outputSpan = output.Memory.AsSpan();

            var index = 0;
            
            foreach (var token in outputSpan)
            {
                Console.WriteLine(
                $"""
                Text: {inputTexts[index++]}
                Input IDs: {token.IDs.AsReadOnlySpan().GetSpanPrintString()}
                Attention Mask: {token.AttentionMask.AsReadOnlySpan().GetSpanPrintString()}
                
                """);
                
                token.Dispose();
            }
            
            
            output.Dispose();
            
            tokenizer.Dispose();

            Console.WriteLine("Done!");
        }
    }
}