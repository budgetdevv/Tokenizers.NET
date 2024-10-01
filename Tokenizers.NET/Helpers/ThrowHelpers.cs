using System;
using System.Diagnostics.CodeAnalysis;

namespace Tokenizers.NET.Helpers
{
    internal static class ThrowHelpers
    {
        // The sole purpose of this class is so that throw code is hoisted into another method,
        // which avoids polluting hot path.
        
        // Also, throw statements prevent inlining, which means the hot path will NOT be inlined otherwise.
        
        // Do NOT use [MethodImpl(MethodImplOptions.NoInlining)] here - It is a de-optimization as the JIT will no longer
        // be able to tell that the method always throw, meaning it will not reorder it to a cold block.
        
        // The JIT will NOT inline this method, as mentioned.
        
        [DoesNotReturn]
        public static void TokenizeBatchInternal_LengthCheckFailed()
        {
            throw new ArgumentException("Output Span / Buffer length must be more than or equal to the input length.");
        }

        [DoesNotReturn]
        public static void UTF8EncodingPirated_GetMaxByteCount_OutOfRange()
        {
            throw new InvalidOperationException("Too many characters. The resulting number of bytes is larger than what can be returned as an int");
        }
        
        [DoesNotReturn]
        public static void UTF8EncodingPirated_GetMaxCharCount_OutOfRange()
        {
            throw new InvalidOperationException("Too many bytes. The resulting number of chars is larger than what can be returned as an int.");
        }
    }
}