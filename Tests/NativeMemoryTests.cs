using Allure.NUnit;
using FluentAssertions;
using Tokenizers.NET.Collections;

namespace Tests
{
    [AllureNUnit]
    public sealed class NativeMemoryTests
    {
        [Test]
        public void WrapBuffer()
        {
            var nativeMemory = new NativeMemory<byte>(69);
            
            var nativeBuffer = nativeMemory.Buffer;
            
            var wrappedMemory = nativeMemory.WrapBuffer(nativeBuffer);

            (nativeMemory == wrappedMemory).Should().BeTrue();
        }
    }
}