using Tokenizers.NET;

namespace Tests
{
    internal static class Configs
    {
        public struct FlorenceConfig: ITokenizerConfig
        {
            public static uint ExpectedMaxInputLength => 1024;
            
            public static uint ExpectedMaxBatches => 5;
            
            public static string TokenizerJsonPath => "FlorenceTokenizer.json";
        }
    }
}