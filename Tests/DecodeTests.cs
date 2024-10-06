using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET;

namespace Tests
{
    [AllureNUnit]
    public sealed class DecodeTests
    {
        private Tokenizer<Configs.FlorenceTokenizer> FlorenceTokenizer;
        
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
        
        [Test]
        public void DecodeMutatingStressTest()
        {
            ref var tokenizer = ref FlorenceTokenizer;
            
            const nuint MAX_VALUE = 5000;
            
            for (nuint i = 1; i <= MAX_VALUE; i++)
            {
                var text = "TeSNFb)]*,h;?R3\"{bU&:1~(D3EBB\"%d[`D_5iKd.Ws6~dnZ_P=+c8BCfK)2=e;''$_^-Rl27owa=g(_Cibpdx!B!xh_8GHk/y$0M,b*sr@_&}BInR\"IB-#=@,Y#Q}HOEQW.Z3V-Z$-\"]zyF6IsEqH!vfoAQ_WtIp9TF4mZ6K;g(t:a0_[;TS)R(U6rCz$M\"/c1CFR1Hm]yV`w\"L-0q#?tffLo|y7:Fmex1)i,Jrfu/{F]4F6j2IdnmJ\\xb3MVBRhz~pBnEdXIX~Vj%2hR O4C *B^`oI6D0R\\2J^C$GDLp;5nrqsPJzUrDysMc-T;B$HKxWZSENX+fRdsSjn{wkIY_lOit?9n1M6lBy98|$K{gWq5~yw=WBVZmkZj*hP.?;T$N'0pUbzf|]wfy:iyFz6mvZ}VN.3S=\\L\"lewh'gtFn\\0ssoo~UfC$e,r(YQ'Tg2_ Kc-Sp1?3AG(}maI]=4}U9G;`ro/T`?^\\{/H/b;-CG0a$t:v'H7";
                
                using var tokenizeResult = tokenizer.Tokenize(text);

                var tokenizedIDs = tokenizeResult.IDs;

                // Console.WriteLine(tokenizedIDs.AsReadOnlySpan().GetSpanPrintString());
                
                using var widenedIDsMemory = tokenizedIDs.Widen();
                
                var widenedIDs = widenedIDsMemory.Buffer;

                // Console.WriteLine(widenedIDs.AsSpan().GetSpanPrintString());
                
                using var decodeOutput = tokenizer.DecodeMutating(widenedIDs, true);

                var x = decodeOutput.ToString();
                
                x.Should().Be(text);
            }
        }
    }
}