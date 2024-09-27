using System;
using System.Diagnostics.CodeAnalysis;

namespace Tokenizers.NET.Helpers
{
    internal static class ThrowHelpers
    {
        [DoesNotReturn]
        public static void TokenizeBatchInternalLengthCheckFailed()
        {
            throw new ArgumentException("Output Span / Buffer length must be more than or equal to the input length.");
        }
    }
}