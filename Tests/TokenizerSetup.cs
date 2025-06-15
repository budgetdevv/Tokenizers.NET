using Tokenizers.NET;

namespace Tests
{
    internal static class TokenizerSetup
    {
        public static Tokenizer BuildFlorenceTokenizer()
        {
            return new TokenizerBuilder()
                .SetExpectedMaxBatches(16)
                .SetExpectedMaxInputLength(1024)
                .SetExceedExpectedMaxBatchesBehavior(ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                .SetTokenizerJsonPath("FlorenceTokenizer.json")
                .Build();
        }

        public static Tokenizer BuildOverflowingTokenizer()
        {
            return new TokenizerBuilder()
                .SetExpectedMaxBatches(16)
                .SetExpectedMaxInputLength(384)
                .SetExceedExpectedMaxBatchesBehavior(ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                .SetTokenizerJsonPath("OverflowingTokenizer.json")
                .Build();
        }
    }
}