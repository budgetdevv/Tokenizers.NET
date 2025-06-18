using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET;
using NativeMemory;
using Tokenizers.NET.Helpers;

namespace Tests
{
    [AllureNUnit]
    public sealed class SIMDHelpersTests
    {
        private Tokenizer FlorenceTokenizer;
        
        // What if Rust code doesn't change?
        [SetUp]
        public void Setup()
        {
            FlorenceTokenizer = TokenizerSetup.BuildFlorenceTokenizer();
        }

        [TearDown]
        public void TearDown()
        {
            FlorenceTokenizer.Dispose();
        }
        
        [Test]
        public void WidenTest()
        {
            const nuint MAX_VALUE = 500;
            
            for (nuint i = 1; i <= MAX_VALUE; i++)
            {
                using var srcBuffer = new NativeMemory<uint>(i);
                using var destBuffer = new NativeMemory<ulong>(i);

                var srcMemory = srcBuffer.Window;
                var destMemory = destBuffer.Window;

                var srcSpan = srcMemory.AsSpan();
                var destSpan = destMemory.AsSpan();
                
                var currentIndex = 0;
                
                foreach (ref var slot in srcSpan)
                {
                    slot = (uint) currentIndex++;
                }

                srcBuffer.Window.Widen(destBuffer.Window);
                
                srcSpan.ToArray().Should().BeEquivalentTo(destSpan.ToArray());
            }
        }
        
        [Test]
        public void NarrowMutating()
        {
            const nuint MAX_VALUE = 500;
            
            for (nuint i = 0; i <= MAX_VALUE; i++)
            {
                using var srcBuffer = new NativeMemory<ulong>(i);

                var srcMemory = srcBuffer.Window;
                
                var srcSpan = srcMemory.AsSpan();

                var currentIndex = 0;
                
                foreach (ref var slot in srcSpan)
                {
                    slot = (ulong) currentIndex++;
                }

                // Copy the values before they get mutated
                var srcMemoryArr = srcSpan.ToArray();
                
                var mutated = srcMemory.NarrowMutating();

                // Console.WriteLine($"Mutated length: {mutated.Length}");
                // Console.WriteLine($"Source length: {srcBuffer.Memory.Length}");

                var srcLength = srcMemory.Length;
                
                (mutated.Length / 2).Should().Be(srcLength);
                
                var mutatedSpan = mutated.AsSpan().Slice(0, (int) srcLength);

                mutatedSpan.ToArray().Should().BeEquivalentTo(srcMemoryArr);
            }
        }

        [Test]
        public void NarrowNonOverlapping()
        {
            const nuint MAX_VALUE = 500;
            
            for (nuint i = 1; i <= MAX_VALUE; i++)
            {
                using var srcBuffer = new NativeMemory<ulong>(i);
                using var destBuffer = new NativeMemory<uint>(i);

                var srcMemory = srcBuffer.Window;
                var destMemory = destBuffer.Window;

                var srcSpan = srcMemory.AsSpan();
                var destSpan = destMemory.AsSpan();

                var currentIndex = 0;
                
                foreach (ref var slot in srcSpan)
                {
                    slot = (uint) currentIndex++;
                }

                srcMemory.NarrowNonOverlapping(destMemory);
                
                srcSpan.ToArray().Should().BeEquivalentTo(destSpan.ToArray());
            }
        }
    }
}