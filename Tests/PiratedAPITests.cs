using System.Text;
using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET.Helpers;

namespace Tests
{
    [AllureNUnit]
    public sealed class PiratedAPITests
    {
        [Test]
        public void GetMaxByteCount()
        {
            const int ITERATIONS = 10_000;
            
            for (int i = 0; i < ITERATIONS; i++)
            {
                UTF8EncodingPirated.GetMaxByteCount(i).Should().Be(Encoding.UTF8.GetMaxByteCount(i));
            }
        }
        
        [Test]
        public void GetMaxCharCount()
        {
            const int ITERATIONS = 10_000;
            
            for (int i = 0; i < ITERATIONS; i++)
            {
                UTF8EncodingPirated.GetMaxCharCount(i).Should().Be(Encoding.UTF8.GetMaxCharCount(i));
            }
        }
    }
}