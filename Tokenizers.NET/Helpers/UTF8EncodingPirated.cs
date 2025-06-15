using System.Diagnostics;
using System.Text;

namespace Tokenizers.NET.Helpers
{
    
    internal static class UTF8EncodingPirated
    {
        // This class is a partial clone of https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Text/UTF8Encoding.cs,abd01a5649677f6c
        
        // It seems like ( In .NET 8 at least ) GetMaxByteCount() and GetMaxCharCount() are not inlined as a result of
        // a de-virtualization failure early on.
        
        // impDevirtualizeCall: Trying to devirtualize virtual call:
        //     class for 'this' is System.Text.Encoding (attrib 21000400)
        //     base method is System.Text.Encoding::GetMaxByteCount
        //     devirt to System.Text.Encoding::GetMaxByteCount -- inexact or not final
        //                [000083] --C-G------                         *  CALLV vt-ind int    System.Text.Encoding:GetMaxByteCount(int):int:this
        //                [000081] --C-------- this                    +--*  RET_EXPR  ref   (for [000080])
        //                [000082] ----------- arg1                    \--*  LCL_VAR   int    V14 tmp11        
        //     Class not final or exact, and method not final
        // Considering guarded devirtualization at IL offset 174 (0xae)
        // No likely class or method, sorry
        // Too many exact classes implementing System.Text.Encoding (-1 > 1)
        // Not guessing; no PGO and no exact classes
        // INLINER: during 'impMarkInlineCandidate' result 'failed this call site' reason 'target not direct' for 'Tokenizers.NET.Tokenizer`1[Codegen.Program+TokenizerConfig]:TokenizeBatchInternal(System.ReadOnlySpan`1[System.String],Tokenizers.NET.Collections.NativeBuffer`1[Tokenizers.NET.TokenizeOutput],ubyte,ubyte):this' calling 'System.Text.Encoding:GetMaxByteCount(int):int:this'
        // INLINER: during 'impMarkInlineCandidate' result 'failed this call site' reason 'target not direct'
        // 
        
        // This class is also modified to specialize for Tokenizer, removing some redundant checks

        /// <summary>
        /// Transcoding to UTF-8 bytes from UTF-16 input chars will result in a maximum 3:1 expansion.
        /// </summary>
        /// <remarks>
        /// Supplementary code points are expanded to UTF-8 from UTF-16 at a 4:2 ratio,
        /// so 3:1 is still the correct value for maximum expansion.
        /// </remarks>
        private const int MAX_UTF8_BYTES_PER_CHAR = 3;

        private static readonly EncoderReplacementFallback FALLBACK = new();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxByteCount(int charCount)
        {
            #if DEBUG
            Debug.Assert(charCount >= 0);
            #endif
            
            // ArgumentOutOfRangeException.ThrowIfNegative(charCount);

            // GetMaxByteCount assumes that the caller might have a stateful Encoder instance. If the
            // Encoder instance already has a captured high surrogate, then one of two things will
            // happen:
            //
            // - The next char is a low surrogate, at which point the two chars together result in 4
            //   UTF-8 bytes in the output; or
            // - The next char is not a low surrogate (or the input reaches EOF), at which point the
            //   standalone captured surrogate will go through the fallback routine.
            //
            // The second case is the worst-case scenario for expansion, so it's what we use for any
            // pessimistic "max byte count" calculation: assume there's a captured surrogate and that
            // it must fall back.

            var byteCount = (long) charCount + 1; // +1 to account for captured surrogate, per above
            
            var encoderFallbackMaxCharCount = FALLBACK.MaxCharCount;
            
            if (encoderFallbackMaxCharCount > 1)
            {
                byteCount *= encoderFallbackMaxCharCount;
            }

            byteCount *= MAX_UTF8_BYTES_PER_CHAR;

            if (byteCount > 0x7fffffff)
            {
                ThrowHelpers.UTF8EncodingPirated_GetMaxByteCount_OutOfRange();
            }
            
            return (int) byteCount;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxCharCount(int byteCount)
        {
            // ArgumentOutOfRangeException.ThrowIfNegative(byteCount);

            // GetMaxCharCount assumes that the caller might have a stateful Decoder instance. If the
            // Decoder instance already has a captured partial UTF-8 subsequence, then one of two
            // thngs will happen:
            //
            // - The next byte(s) won't complete the subsequence but will instead be consumed into
            //   the Decoder's internal state, resulting in no character output; or
            // - The next byte(s) will complete the subsequence, and the previously captured
            //   subsequence and the next byte(s) will result in 1 - 2 chars output; or
            // - The captured subsequence will be treated as a singular ill-formed subsequence, at
            //   which point the captured subsequence will go through the fallback routine.
            //   (See The Unicode Standard, Sec. 3.9 for more information on this.)
            //
            // The third case is the worst-case scenario for expansion, since it means 0 bytes of
            // new input could cause any existing captured state to expand via fallback. So it's
            // what we'll use for any pessimistic "max char count" calculation.

            var charCount = ((long) byteCount + 1); // +1 to account for captured subsequence, as above
            
            var decoderFallbackMaxCharCount = FALLBACK.MaxCharCount;
            
            // Non-shortest form would fall back, so get max count from fallback.
            // So would 11... followed by 11..., so you could fall back every byte
            if (decoderFallbackMaxCharCount > 1)
            {
                charCount *= decoderFallbackMaxCharCount;
            }

            if (charCount > 0x7fffffff)
            {
                ThrowHelpers.UTF8EncodingPirated_GetMaxCharCount_OutOfRange();
            }

            return (int) charCount;
        }
    }
}
