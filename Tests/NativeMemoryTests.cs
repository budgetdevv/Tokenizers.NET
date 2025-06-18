using Allure.NUnit;
using FluentAssertions;
using NativeMemory;

namespace Tests
{
    [AllureNUnit]
    public sealed class NativeMemoryTests
    {
        [Test]
        public void WrapBuffer()
        {
            using var nativeMemory = new NativeMemory<byte>(69);
            
            var nativeBuffer = nativeMemory.Window;
            
            var wrappedMemory = NativeMemory<byte>.WrapBufferUnsafely(nativeBuffer);

            (nativeMemory.Window == wrappedMemory.Window).Should().BeTrue();
        }
    }
}