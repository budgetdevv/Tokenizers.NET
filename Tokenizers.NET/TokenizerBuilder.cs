using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Tokenizers.NET.Collections;

namespace Tokenizers.NET
{
    public enum ExceedExpectedMaxBatchesBehavior
    {
        AllocateBuffer,
        // Discard, // TODO: Implement this
    }

    public sealed class TokenizerBuilder
    {
        private static readonly string DEFAULT_TOKENIZER_JSON_CACHE_DIRECTORY = Path.Combine(
            AppContext.BaseDirectory,
            "TokenizersJSONCache"
        );

        internal uint ExpectedMaxInputLength = 1024, ExpectedMaxBatches = 16;

        public ExceedExpectedMaxBatchesBehavior ExceedExpectedMaxBatchesBehavior = ExceedExpectedMaxBatchesBehavior.AllocateBuffer;

        internal string? TokenizerJsonPath = null;

        internal byte[]? RawTokenizerData = null;

        public TokenizerBuilder SetExpectedMaxInputLength(uint expectedMaxInputLength)
        {
            ExpectedMaxInputLength = expectedMaxInputLength;
            return this;
        }

        public TokenizerBuilder SetExpectedMaxBatches(uint expectedMaxBatches)
        {
            ExpectedMaxBatches = expectedMaxBatches;
            return this;
        }

        public TokenizerBuilder SetExceedExpectedMaxBatchesBehavior(ExceedExpectedMaxBatchesBehavior exceedExpectedMaxBatchesBehavior)
        {
            ExceedExpectedMaxBatchesBehavior = exceedExpectedMaxBatchesBehavior;
            return this;
        }

        public TokenizerBuilder SetTokenizerJsonPath(string tokenizerJsonPath)
        {
            TokenizerJsonPath = tokenizerJsonPath;
            return this;
        }

        public TokenizerBuilder SetRawTokenizerData(byte[] rawTokenizerData)
        {
            RawTokenizerData = rawTokenizerData;
            return this;
        }

        public async ValueTask<TokenizerBuilder> DownloadFromHuggingFaceRepoAsync(
            string huggingFaceRepoName,
            string? cacheDirectory = null,
            bool forceDownload = false)
        {
            cacheDirectory ??= DEFAULT_TOKENIZER_JSON_CACHE_DIRECTORY;

            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            var tokenizerJSONFileName = $"{huggingFaceRepoName.Replace('/', '_')}.json";

            var tokenizerJsonPath = Path.Combine(cacheDirectory, tokenizerJSONFileName);

            TokenizerJsonPath = tokenizerJsonPath;

            var shouldSkip = File.Exists(tokenizerJsonPath) && !forceDownload;

            if (shouldSkip)
            {
                return this;
            }

            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Add("User-Agent", "Tokenizers.NET");

            var downloadURL = $"https://huggingface.co/{huggingFaceRepoName}/resolve/main/tokenizer.json?download=true";

            var rawTokenizerData = await httpClient.GetByteArrayAsync(downloadURL);

            await File.WriteAllBytesAsync(tokenizerJsonPath, rawTokenizerData);

            RawTokenizerData = rawTokenizerData;

            return this;
        }

        internal TokenizerConfig BuildConfig()
        {
            return new(this);
        }

        public Tokenizer Build()
        {
            return new(this);
        }
    }

    public readonly struct TokenizerConfig
    {
        public readonly uint ExpectedMaxInputLength, ExpectedMaxBatches;

        public readonly ExceedExpectedMaxBatchesBehavior ExceedExpectedMaxBatchesBehavior;

        public readonly string? TokenizerJsonPath;

        public readonly NativeMemory<byte> RawTokenizerData;

        public readonly Truncation? Truncation;

        public readonly bool Truncates;

        internal TokenizerConfig(TokenizerBuilder builder)
        {
            ExpectedMaxInputLength = builder.ExpectedMaxInputLength;

            ExpectedMaxBatches = builder.ExpectedMaxBatches;

            ExceedExpectedMaxBatchesBehavior = builder.ExceedExpectedMaxBatchesBehavior;

            var tokenizerJsonPath = TokenizerJsonPath = builder.TokenizerJsonPath;

            var rawTokenizerDataArr = builder.RawTokenizerData;

            // Let it throw if both are null
            rawTokenizerDataArr ??= File.ReadAllBytes(tokenizerJsonPath!);

            // TODO: Consider mmap instead of heap allocation.

            var rawTokenizerData = RawTokenizerData = new(
                (nuint) rawTokenizerDataArr.Length
            );

            var rawTokenizerDataSpan = rawTokenizerData.Buffer.AsSpan();

            rawTokenizerDataArr.CopyTo(rawTokenizerDataSpan);

            var tokenizerData = JsonSerializer.Deserialize<TokenizerData>(rawTokenizerDataSpan);

            var truncation = Truncation = tokenizerData.Truncation;

            Truncates = truncation != null;
        }
    }
}