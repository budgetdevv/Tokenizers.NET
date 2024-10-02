using System.Text;

namespace Tests
{
    public static class TestHelpers
    {
        public static string GetSpanPrintString<T>(this Span<T> span)
        {
            return GetSpanPrintString((ReadOnlySpan<T>) span);
        }
        
        public static string GetSpanPrintString<T>(this ReadOnlySpan<T> span)
        {
            return span.ToArray().GetArrPrintString();
        }
        
        public static string GetArrPrintString<T>(this T[] arr)
        {
            // It has to include commas and the array brackets as well...
            var stringBuilder = new StringBuilder(arr.Length * 2);

            stringBuilder.Append('[');
            
            const string SEPARATOR = ", ";
            
            foreach (var item in arr)
            {
                stringBuilder.Append(item);
                stringBuilder.Append(SEPARATOR);
            }
            
            var separatorLength = SEPARATOR.Length;
            stringBuilder.Remove(stringBuilder.Length - separatorLength, separatorLength);
            
            stringBuilder.Append(']');
            
            return stringBuilder.ToString();
        }
        
        private static readonly Random RANDOM = new();

        public static string AllocateStringWithRandomChars(int length)
        {
            var random = RANDOM;
            
            return string.Create(length, length, (charSpan, _) =>
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
        
        public static IEnumerable<string> GenerateBatch(int textLength, int batchSize)
        {
            for (var i = 0; i < batchSize; i++)
            {
                yield return AllocateStringWithRandomChars(textLength);
            }
        }
    }
}