using System.Runtime.InteropServices;
using Allure.NUnit;
using FluentAssertions;
using NativeMemory;
using Tokenizers.NET;
using Tokenizers.NET.Outputs;

namespace Tests
{
    [AllureNUnit]
    public sealed class EncodeTests
    {
        private Tokenizer FlorenceTokenizer;
        
        private Tokenizer OverflowingTokenizer;
        
        [SetUp]
        public void Setup()
        {
            FlorenceTokenizer = TokenizerSetup.BuildFlorenceTokenizer();
            OverflowingTokenizer = TokenizerSetup.BuildOverflowingTokenizer();
        }

        [TearDown]
        public void TearDown()
        {
            FlorenceTokenizer.Dispose();
            OverflowingTokenizer.Dispose();
        }
        
        [Test]
        public void EncodeOverflowing()
        {
            ref var tokenizer = ref OverflowingTokenizer;

            const nuint MIN_OVERFLOWING_SEGMENTS = 3;

            var expectedMaxInputLength = (nuint) tokenizer.Config.ExpectedMaxInputLength;
            
            var length = expectedMaxInputLength;

            string text;
            
            TokenizeOutput tokenizeResult;
            
            MemoryWindow<TokenizeOutputOverflowedToken> overflowingTokens;

            nuint numOverflowingTokensSegments;
            
            while (true)
            {
                text = TestHelpers.AllocateStringWithRandomChars((int) length);
                
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
                overflowingToken.TokenTypeIDs.Length.Should().Be(expectedMaxInputLength);
                
                ids.AddRange(overflowingIDs.AsReadOnlySpan());

                // Console.WriteLine(overflowingToken.IDs.AsReadOnlySpan().GetSpanPrintString());
            }
            
            ids.Count.Should().Be(expectedTotalIDLength);
            
            var idsSpan = CollectionsMarshal.AsSpan(ids);

            using var gatherMemory = new NativeMemory<uint>((nuint) expectedTotalIDLength);
            
            var gatherBuffer = gatherMemory.Window;
            
            tokenizeResult.GatherIDsInclusiveOfOverflowing(gatherBuffer, out var gatheredLength);
            
            gatheredLength.Should().Be((nuint) expectedTotalIDLength);
            
            var gatheredIDs = gatherBuffer.AsSpan();
            
            gatheredIDs.SequenceEqual(idsSpan).Should().BeTrue();
            
            using var decodeOutput = tokenizer.Decode(idsSpan, skipSpecialTokens: true);
            
            var decodedText = decodeOutput.ToString();
            
            decodedText.Should().Be(text);
            
            tokenizeResult.Dispose();
        }
        
        private static ulong[] WidenSafely(MemoryWindow<uint> source)
        {
            var sourceSpan = source.AsReadOnlySpan();
            
            var widened = new ulong[sourceSpan.Length];
            
            for (var i = 0; i < sourceSpan.Length; i++)
            {
                widened[i] = sourceSpan[i];
            }

            return widened;
        }

        [Test]
        public void EncodeOverflowingWiden()
        {
            ref var tokenizer = ref OverflowingTokenizer;
            
            var expectedMaxInputLength = (nuint) tokenizer.Config.ExpectedMaxInputLength;
            
            var length = expectedMaxInputLength;

            string text;
            
            TokenizeOutput tokenizeResult;
            
            MemoryWindow<TokenizeOutputOverflowedToken> overflowingTokens;

            nuint numOverflowingTokensSegments;
            
            while (true)
            {
                text = TestHelpers.AllocateStringWithRandomChars((int) length);
                
                tokenizeResult = tokenizer.Tokenize(text);
                
                overflowingTokens = tokenizeResult.OverflowingTokens;

                numOverflowingTokensSegments = overflowingTokens.Length;
                
                if (numOverflowingTokensSegments > 0)
                {
                    break;
                }
                
                tokenizeResult.Dispose();
                length *= 2;
            }
            
            var expectedTotalIDLength = (int) (expectedMaxInputLength * 2);
            
            var ids = new List<ulong>(expectedTotalIDLength);
            var attentionMask = new List<ulong>(expectedTotalIDLength);
            var specialTokensMask = new List<ulong>(expectedTotalIDLength);
            var tokenTypeIDs = new List<ulong>(expectedTotalIDLength);
            
            ids.AddRange(WidenSafely(tokenizeResult.IDs));
            
            attentionMask.AddRange(WidenSafely(tokenizeResult.AttentionMask));
            
            specialTokensMask.AddRange(WidenSafely(tokenizeResult.SpecialTokensMask));
            
            tokenTypeIDs.AddRange(WidenSafely(tokenizeResult.TokenTypeIDs));
            
            foreach (var overflowingToken in overflowingTokens.AsReadOnlySpan())
            {
                var overflowingIDs = overflowingToken.IDs;
                var overflowingAttentionMask = overflowingToken.AttentionMask;
                var overflowingSpecialTokensMask = overflowingToken.SpecialTokensMask;
                var overflowingTokenTypeIDs = overflowingToken.TokenTypeIDs;
                
                overflowingIDs.Length.Should().Be(expectedMaxInputLength);
                overflowingAttentionMask.Length.Should().Be(expectedMaxInputLength);
                overflowingSpecialTokensMask.Length.Should().Be(expectedMaxInputLength);
                overflowingTokenTypeIDs.Length.Should().Be(expectedMaxInputLength);
                
                ids.AddRange(WidenSafely(overflowingIDs));
                attentionMask.AddRange(WidenSafely(overflowingAttentionMask));
                specialTokensMask.AddRange(WidenSafely(overflowingSpecialTokensMask));
                tokenTypeIDs.AddRange(WidenSafely(overflowingTokenTypeIDs));
            }
            
            ids.Count.Should().Be(expectedTotalIDLength);
            attentionMask.Count.Should().Be(expectedTotalIDLength);
            specialTokensMask.Count.Should().Be(expectedTotalIDLength);
            tokenTypeIDs.Count.Should().Be(expectedTotalIDLength);
            
            var idsSpan = CollectionsMarshal.AsSpan(ids);
            var attentionMaskSpan = CollectionsMarshal.AsSpan(attentionMask);
            var specialTokensMaskSpan = CollectionsMarshal.AsSpan(specialTokensMask);
            var tokenTypeIDsSpan = CollectionsMarshal.AsSpan(tokenTypeIDs);

            using var idGatherResult = tokenizeResult.GatherAndWidenIDsInclusiveOfOverflowing();
            using var attentionMaskGatherResult = tokenizeResult.GatherAndWidenAttentionMaskInclusiveOfOverflowing();
            using var specialTokensMaskGatherResult = tokenizeResult.GatherAndWidenSpecialTokensMaskInclusiveOfOverflowing();
            using var tokenTypeIDsGatherResult = tokenizeResult.GatherAndWidenTokenTypeIDsInclusiveOfOverflowing();
            
            var gatheredIDs = idGatherResult.Window.AsSpan();
            var gatheredAttentionMask = attentionMaskGatherResult.Window.AsSpan();
            var gatheredSpecialTokensMask = specialTokensMaskGatherResult.Window.AsSpan();
            var gatheredTokenTypeIDs = tokenTypeIDsGatherResult.Window.AsSpan();
            
            gatheredIDs.SequenceEqual(idsSpan).Should().BeTrue();
            gatheredAttentionMask.SequenceEqual(attentionMaskSpan).Should().BeTrue();
            gatheredSpecialTokensMask.SequenceEqual(specialTokensMaskSpan).Should().BeTrue();
            gatheredTokenTypeIDs.SequenceEqual(tokenTypeIDsSpan).Should().BeTrue();
            
            tokenizeResult.Dispose();
        }

        [Test]
        public void EncodeWithMaxManagedLength()
        {
            ref var tokenizer = ref FlorenceTokenizer;
            
            var config = tokenizer.Config;
            
            var maxManagedLength = config.ExpectedMaxInputLength * config.ExpectedMaxBatches;
            
            var text = TestHelpers.AllocateStringWithRandomChars((int) maxManagedLength);

            const int ITERATIONS = 500;
            
            for (var i = 0; i < ITERATIONS; i++)
            {
                var tokenizeResult = tokenizer.Tokenize(text);
                
                tokenizeResult.Dispose();
            }
        }
        
        [Test]
        public void EncodeDecodeBatched()
        {
            ref var tokenizer = ref FlorenceTokenizer;
            
            var config = tokenizer.Config;
            
            var maxBatches = (int) config.ExpectedMaxBatches;
            
            var maxInputLength = (int) config.ExpectedMaxInputLength;

            const int ITERATIONS = 500;
            
            for (var i = 0; i < ITERATIONS; i++)
            {
                var texts = TestHelpers
                    .GenerateBatch(maxInputLength, maxBatches)
                    .ToArray();
                
                var texts2 = TestHelpers
                    .GenerateBatch(maxInputLength * 2, maxBatches)
                    .ToArray();

                var allTexts = texts.Concat(texts2).ToArray();
                
                using var tokenizeResults = tokenizer.TokenizeBatch(allTexts);

                var currentIndex = 0;
                
                foreach (var tokenizeResult in tokenizeResults.Window)
                {
                    var ids = tokenizeResult.IDs;
                    
                    using var decodeOutput = tokenizer.Decode(ids, skipSpecialTokens: true);
                    
                    var decodedText = decodeOutput.ToString();
                    
                    decodedText.Should().Be(allTexts[currentIndex++]);
                    
                    tokenizeResult.Dispose();
                }
            }
        }
    }
}