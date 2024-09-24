using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET;
using Tokenizers.NET.Collections;

namespace Tests
{
    [AllureNUnit]
    public sealed class SIMDHelpersTests
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
        
        [Test]
        public void WidenTest()
        {
            const nuint MAX_VALUE = 500;
            
            for (nuint i = 1; i <= MAX_VALUE; i++)
            {
                using var srcBuffer = new NativeMemory<uint>(i);
                using var destBuffer = new NativeMemory<ulong>(i);

                var srcMemory = srcBuffer.Buffer;
                var destMemory = destBuffer.Buffer;

                var srcSpan = srcMemory.AsSpan();
                var destSpan = destMemory.AsSpan();
                
                var currentIndex = 0;
                
                foreach (ref var slot in srcSpan)
                {
                    slot = (uint) currentIndex++;
                }

                srcBuffer.Buffer.AsReadOnly().Widen(destBuffer.Buffer);
                
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

                var srcMemory = srcBuffer.Buffer;
                
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

                var srcMemory = srcBuffer.Buffer;
                var destMemory = destBuffer.Buffer;

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