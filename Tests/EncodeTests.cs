using System.Runtime.InteropServices;
using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET;
using Tokenizers.NET.Collections;

namespace Tests
{
    [AllureNUnit]
    public sealed class EncodeTests
    {
        private Tokenizer<Configs.FlorenceConfig> FlorenceTokenizer;
        
        private Tokenizer<Configs.OverflowingTokenizer> OverflowingTokenizer;
        
        [SetUp]
        public void Setup()
        {
            FlorenceTokenizer = new();
            OverflowingTokenizer = new();
        }

        [TearDown]
        public void TearDown()
        {
            FlorenceTokenizer.Dispose();
            OverflowingTokenizer.Dispose();
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
        public void EncodeOverflowing()
        {
            ref var tokenizer = ref OverflowingTokenizer;

            const nuint MIN_OVERFLOWING_SEGMENTS = 3;

            var expectedMaxInputLength = (nuint) Configs.OverflowingTokenizer.ExpectedMaxInputLength;
            
            var length = expectedMaxInputLength;

            string text;
            
            TokenizeOutput tokenizeResult;
            
            ReadOnlyNativeBuffer<TokenizeOutputOverflowedToken> overflowingTokens;

            nuint numOverflowingTokensSegments;
            
            while (true)
            {
                text = AllocateStringWithRandomChars((int) length);
                
                tokenizeResult = tokenizer.Tokenize(text);
                
                overflowingTokens = tokenizeResult.OverflowingTokens;

                numOverflowingTokensSegments = overflowingTokens.Length;
                
                if (numOverflowingTokensSegments >= MIN_OVERFLOWING_SEGMENTS)
                {
                    break;
                }
                
                tokenizeResult.Dispose();
                length *= 2;
            }

            // Console.WriteLine($"Text: {text}");

            var expectedTotalIDLength = (int) (expectedMaxInputLength * (numOverflowingTokensSegments + 1));
            
            var ids = new List<uint>(expectedTotalIDLength);
            
            ids.AddRange(tokenizeResult.IDs.AsReadOnlySpan());
            
            foreach (var overflowingToken in overflowingTokens.AsReadOnlySpan())
            {
                var overflowingIDs = overflowingToken.IDs;
                
                overflowingIDs.Length.Should().Be(expectedMaxInputLength);
                overflowingToken.AttentionMask.Length.Should().Be(expectedMaxInputLength);
                overflowingToken.SpecialTokensMask.Length.Should().Be(expectedMaxInputLength);
                
                ids.AddRange(overflowingIDs.AsReadOnlySpan());

                // Console.WriteLine(overflowingToken.IDs.AsReadOnlySpan().GetSpanPrintString());
            }
            
            ids.Count.Should().Be(expectedTotalIDLength);
            
            var idsSpan = CollectionsMarshal.AsSpan(ids);

            using var gatherMemory = new NativeMemory<uint>((nuint) expectedTotalIDLength);
            
            var gatherBuffer = gatherMemory.Memory;
            
            tokenizeResult.GatherIDsInclusiveOfOverflowing(gatherBuffer, performRangeCheck: true);
            
            var gatheredIDs = gatherBuffer.AsSpan();
            
            gatheredIDs.SequenceEqual(idsSpan).Should().BeTrue();
            
            using var decodeOutput = tokenizer.Decode(idsSpan, skipSpecialTokens: true);
            
            var decodedText = decodeOutput.ToString();
            
            decodedText.Should().Be(text);
            
            tokenizeResult.Dispose();
        }
    }
}