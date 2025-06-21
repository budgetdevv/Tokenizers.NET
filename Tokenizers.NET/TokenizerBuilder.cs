using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NativeMemory;

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

        internal ExceedExpectedMaxBatchesBehavior ExceedExpectedMaxBatchesBehavior = ExceedExpectedMaxBatchesBehavior.AllocateBuffer;

        internal uint ExpectedMaxInputLength = 1024, ExpectedMaxBatches = 16;

        private string? TokenizerJsonPath = null;

        private byte[]? RawTokenizerData = null;

        private Func<TokenizerData, TokenizerData>? ModifyTokenizerConfigFunc = null;

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

        public TokenizerBuilder ModifyTokenizerConfig(Func<TokenizerData, TokenizerData> modifyTokenizerConfigFunc)
        {
            ModifyTokenizerConfigFunc = modifyTokenizerConfigFunc;
            return this;
        }

        internal TokenizerConfig BuildConfig(out NativeMemory<byte> rawTokenizerData)
        {
            var tokenizerJsonPath = TokenizerJsonPath;

            var rawTokenizerDataArr = RawTokenizerData;

            // Let it throw if both are null
            rawTokenizerDataArr ??= File.ReadAllBytes(tokenizerJsonPath!);

            var tokenizerData = JsonSerializer.Deserialize<TokenizerData>(
                rawTokenizerDataArr
            )!;

            var modifyFunc = ModifyTokenizerConfigFunc;

            if (modifyFunc != null)
            {
                tokenizerData = modifyFunc(tokenizerData);

                rawTokenizerDataArr = JsonSerializer.SerializeToUtf8Bytes(
                    tokenizerData
                );
            }

            rawTokenizerData = new(
                (nuint) rawTokenizerDataArr.Length
            );

            var rawTokenizerDataSpan = rawTokenizerData.Window.AsSpan();

            rawTokenizerDataArr.CopyTo(rawTokenizerDataSpan);

            return new(this, tokenizerData);
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

        public readonly Truncation? Truncation;

        public bool Truncates => Truncation != null;

        internal TokenizerConfig(TokenizerBuilder builder, TokenizerData tokenizerData)
        {
            ExpectedMaxInputLength = builder.ExpectedMaxInputLength;

            ExpectedMaxBatches = builder.ExpectedMaxBatches;

            ExceedExpectedMaxBatchesBehavior = builder.ExceedExpectedMaxBatchesBehavior;

            Truncation = tokenizerData.Truncation;
        }
    }
}