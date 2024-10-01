using Tokenizers.NET;

namespace Tests
{
    internal static class Configs
    {
        public struct FlorenceTokenizer: Tokenizer.IConfig
        {
            public static Tokenizer.BuiltConfig BuiltConfig =>
                new Tokenizer.ConfigBuilder()
                    .SetExpectedMaxBatches(16)
                    .SetExpectedMaxInputLength(1024)
                    .SetExceedExpectedMaxBatchesBehavior(Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                    .SetTokenizerJsonPath("FlorenceTokenizer.json")
                    .Build();
        }
        
        public struct OverflowingTokenizer: Tokenizer.IConfig
        {
            public static Tokenizer.BuiltConfig BuiltConfig =>
                new Tokenizer.ConfigBuilder()
                    .SetExpectedMaxBatches(16)
                    .SetExpectedMaxInputLength(384)
                    .SetExceedExpectedMaxBatchesBehavior(Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                    .SetTokenizerJsonPath("OverflowingTokenizer.json")
                    .Build();
        }
    }
}