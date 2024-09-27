using Tokenizers.NET;

namespace Tests
{
    internal static class Configs
    {
        public struct FlorenceTokenizer: Tokenizer.IConfig
        {
            private static readonly Tokenizer.BuiltConfig BUILT_CONFIG =
                new Tokenizer.ConfigBuilder()
                    .SetExpectedMaxBatches(1024)
                    .SetExpectedMaxInputLength(16)
                    .SetExceedExpectedMaxBatchesBehavior(Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                    .SetTokenizerJsonPath("FlorenceTokenizer.json")
                    .Build();

            public static Tokenizer.BuiltConfig BuiltConfig => BUILT_CONFIG;
        }
        
        public struct OverflowingTokenizer: Tokenizer.IConfig
        {
            private static readonly Tokenizer.BuiltConfig BUILT_CONFIG =
                new Tokenizer.ConfigBuilder()
                    .SetExpectedMaxBatches(16)
                    .SetExpectedMaxInputLength(384)
                    .SetExceedExpectedMaxBatchesBehavior(Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                    .SetTokenizerJsonPath("OverflowingTokenizer.json")
                    .Build();

            public static Tokenizer.BuiltConfig BuiltConfig => BUILT_CONFIG;
        }
    }
}