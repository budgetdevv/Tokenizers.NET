using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET;
using Tokenizers.NET.Collections;

namespace Tests
{
    [AllureNUnit]
    public sealed class SIMDHelpersTests
    {
        private struct Config: ITokenizerConfig
        {
            public static uint ExpectedMaxInputLength => 1024;
            
            public static uint ExpectedMaxBatches => 5;
            
            public static string TokenizerJsonPath => "FlorenceTokenizer.json";
        }
        
        private Tokenizer<Config> Tokenizer;
        
        [SetUp]
        public void Setup()
        {
            Tokenizer = new();
        }

        [TearDown]
        public void TearDown()
        {
            Tokenizer.Dispose();
        }
        
        [Test]
        public void WidenTest()
        {
            const nuint MAX_VALUE = 500;
            
            for (nuint i = 1; i <= MAX_VALUE; i++)
            {
                using var srcBuffer = new NativeMemory<uint>(i);
                using var destBuffer = new NativeMemory<ulong>(i);

                var srcMemory = srcBuffer.Memory.AsSpan();
                var destMemory = destBuffer.Memory.AsSpan();

                var currentIndex = 0;
                
                foreach (ref var slot in srcMemory)
                {
                    slot = (uint) currentIndex++;
                }

                srcBuffer.Memory.AsReadOnly().Widen(destBuffer.Memory);
                
                srcMemory.ToArray().Should().BeEquivalentTo(destMemory.ToArray());
            }
        }
    }
}