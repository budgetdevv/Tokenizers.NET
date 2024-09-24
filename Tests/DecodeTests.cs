using Allure.NUnit;
using FluentAssertions;
using Sample;
using Tokenizers.NET;
using Tokenizers.NET.Collections;

namespace Tests
{
    [AllureNUnit]
    public sealed class DecodeTests
    {
        private Tokenizer<Configs.FlorenceConfig> FlorenceTokenizer;
        
        // What if Rust code doesn't change?
        [SetUp]
        public void Setup()
        {
            FlorenceTokenizer = new();
        }

        [TearDown]
        public void TearDown()
        {
            FlorenceTokenizer.Dispose();
        }

        private static string AllocateStringWithRandomChars(int length)
        {
            var random = new Random((int) DateTime.Now.Ticks);
            
            return string.Create(length, length, (charSpan,_ ) =>
            {
                for (var i = 0; i < charSpan.Length; i++)
                {
                    while (true)
                    {
                        // https://www.asciitable.com/
                        var generatedChar = (char) random.Next(32, 126 + 1);
                    
                        // Make sure it doesn't accidentally generate special tokens such as <s>
                        if (generatedChar is '<' or '>')
                        {
                            continue;
                        }
                        
                        charSpan[i] = generatedChar;

                        break;
                    }
                }
            });
        }
        
        [Test]
        public void DecodeMutating()
        {
            ref var tokenizer = ref FlorenceTokenizer;
            
            const nuint MAX_VALUE = 500;
            
            for (nuint i = 1; i <= MAX_VALUE; i++)
            {
                var text = AllocateStringWithRandomChars((int) i);
                
                using var tokenizeResult = tokenizer.Tokenize(text);

                var tokenizedIDs = tokenizeResult.IDs;

                // Console.WriteLine(tokenizedIDs.AsReadOnlySpan().GetSpanPrintString());
                
                using var widenedIDsMemory = tokenizedIDs.Widen();
                
                var widenedIDs = widenedIDsMemory.Buffer;

                // Console.WriteLine(widenedIDs.AsSpan().GetSpanPrintString());
                
                using var decodeOutput = tokenizer.DecodeMutating(widenedIDs, true);
                
                decodeOutput.ToString().Should().Be(text);
            }
        }
    }
}