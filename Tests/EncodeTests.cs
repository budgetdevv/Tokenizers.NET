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
            
            tokenizeResult.GatherIDsInclusiveOfOverflowing(gatherBuffer);
            
            var gatheredIDs = gatherBuffer.AsSpan();
            
            gatheredIDs.SequenceEqual(idsSpan).Should().BeTrue();
            
            using var decodeOutput = tokenizer.Decode(idsSpan, skipSpecialTokens: true);
            
            var decodedText = decodeOutput.ToString();
            
            decodedText.Should().Be(text);
            
            tokenizeResult.Dispose();
        }
        
        private static ulong[] WidenSafely(ReadOnlyNativeBuffer<uint> source)
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
            
            ids.AddRange(WidenSafely(tokenizeResult.IDs));
            
            attentionMask.AddRange(WidenSafely(tokenizeResult.AttentionMask));
            
            specialTokensMask.AddRange(WidenSafely(tokenizeResult.SpecialTokensMask));
            
            foreach (var overflowingToken in overflowingTokens.AsReadOnlySpan())
            {
                var overflowingIDs = overflowingToken.IDs;
                var overflowingAttentionMask = overflowingToken.AttentionMask;
                var overflowingSpecialTokensMask = overflowingToken.SpecialTokensMask;
                
                overflowingIDs.Length.Should().Be(expectedMaxInputLength);
                overflowingAttentionMask.Length.Should().Be(expectedMaxInputLength);
                overflowingSpecialTokensMask.Length.Should().Be(expectedMaxInputLength);
                
                ids.AddRange(WidenSafely(overflowingIDs));
                attentionMask.AddRange(WidenSafely(overflowingAttentionMask));
                specialTokensMask.AddRange(WidenSafely(overflowingSpecialTokensMask));
            }
            
            ids.Count.Should().Be(expectedTotalIDLength);
            attentionMask.Count.Should().Be(expectedTotalIDLength);
            specialTokensMask.Count.Should().Be(expectedTotalIDLength);
            
            var idsSpan = CollectionsMarshal.AsSpan(ids);
            var attentionMaskSpan = CollectionsMarshal.AsSpan(attentionMask);
            var specialTokensMaskSpan = CollectionsMarshal.AsSpan(specialTokensMask);

            using var idGatherResult = tokenizeResult.GatherAndWidenIDsInclusiveOfOverflowing();
            using var attentionMaskGatherResult = tokenizeResult.GatherAndWidenAttentionMaskInclusiveOfOverflowing();
            using var specialTokensMaskGatherResult = tokenizeResult.GatherAndWidenSpecialTokensMaskInclusiveOfOverflowing();
            
            var gatheredIDs = idGatherResult.Memory.AsSpan();
            var gatheredAttentionMask = attentionMaskGatherResult.Memory.AsSpan();
            var gatheredSpecialTokensMask = specialTokensMaskGatherResult.Memory.AsSpan();
            
            gatheredIDs.SequenceEqual(idsSpan).Should().BeTrue();
            gatheredAttentionMask.SequenceEqual(attentionMaskSpan).Should().BeTrue();
            gatheredSpecialTokensMask.SequenceEqual(specialTokensMaskSpan).Should().BeTrue();
            
            tokenizeResult.Dispose();
        }
    }
}