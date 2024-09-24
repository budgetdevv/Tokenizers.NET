using Tokenizers.NET;

namespace Tests
{
    internal static class Configs
    {
        public struct FlorenceConfig: ITokenizerConfig
        {
            public static uint ExpectedMaxInputLength => 1024;

            public static uint ExpectedMaxBatches => 2;
            
            public static string TokenizerJsonPath => "FlorenceTokenizer.json";
        }
        
        public struct OverflowingTokenizer: ITokenizerConfig
        {
            public static uint ExpectedMaxInputLength => 384;

            public static uint ExpectedMaxBatches => 2;
            
            public static string TokenizerJsonPath => "OverflowingTokenizer.json";
        }
    }
}