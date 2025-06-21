using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tokenizers.NET
{
    // Note that Rust uses usize for some of the fields,
    // which is the equivalent of nuint in C#.

    // However, serializing to nuint is not supported,
    // so we assume "worst" case and use ulong instead.

    // It should successfully deserialize into usize.

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TruncationDirection
    {
        // https://docs.rs/tokenizers/latest/tokenizers/utils/truncation/enum.TruncationDirection.html

        // Avoid using nameof() here, in case we decide to change the enum names in the future

        [EnumMember(Value = "Left")]
        Left,
        [EnumMember(Value = "Right")]
        Right
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TruncationStrategy
    {
        // https://docs.rs/tokenizers/latest/tokenizers/utils/truncation/enum.TruncationStrategy.html

        // Avoid using nameof() here, in case we decide to change the enum names in the future

        [EnumMember(Value = "LongestFirst")]
        LongestFirst,
        [EnumMember(Value = "OnlyFirst")]
        OnlyFirst,
        [EnumMember(Value = "OnlySecond")]
        OnlySecond,
    }

    public sealed class Truncation(
        ulong maxLength,
        ulong stride = 0,
        TruncationDirection direction = TruncationDirection.Right,
        TruncationStrategy strategy = TruncationStrategy.LongestFirst)
    {
        // https://docs.rs/tokenizers/latest/tokenizers/utils/truncation/struct.TruncationParams.html
        // For default values: https://huggingface.co/docs/tokenizers/en/api/tokenizer#tokenizers.Tokenizer.enable_truncation

        [JsonPropertyName("direction")]
        public TruncationDirection Direction { get; set; } = direction;

        [JsonPropertyName("max_length")]
        public ulong MaxLength { get; set; } = maxLength;

        [JsonPropertyName("strategy")]
        public TruncationStrategy Strategy { get; set; } = strategy;

        [JsonPropertyName("stride")]
        public ulong Stride { get; set; } = stride;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaddingDirection
    {
        // https://docs.rs/tokenizers/latest/tokenizers/utils/padding/enum.PaddingDirection.html

        // Avoid using nameof() here, in case we decide to change the enum names in the future

        [EnumMember(Value = "Left")]
        Left,
        [EnumMember(Value = "Right")]
        Right
    }
            
    public sealed class Padding(
        Padding.StrategyBase? strategy,
        PaddingDirection direction = PaddingDirection.Right,
        ulong? padToMultipleOf = null,
        uint padID = 0,
        uint padTypeID = 0,
        string padToken = "[PAD]")
    {
        // https://docs.rs/tokenizers/latest/tokenizers/utils/padding/struct.PaddingParams.html
        // For default values: https://huggingface.co/docs/tokenizers/en/api/tokenizer#tokenizers.Tokenizer.enable_padding

        [JsonConverter(typeof(Converter))]
        public abstract class StrategyBase
        {
            public sealed class Converter: JsonConverter<StrategyBase>
            {
                public override StrategyBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var value = reader.GetString();

                        if (value == BatchLongestStrategy.VALUE)
                        {
                            return new BatchLongestStrategy();
                        }
                    }

                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        return JsonSerializer.Deserialize<FixedStrategy>(ref reader, options);
                    }

                    throw new JsonException("Invalid padding strategy format.");
                }

                public override void Write(Utf8JsonWriter writer, StrategyBase value, JsonSerializerOptions options)
                {
                    if (value is FixedStrategy fixedStrategy)
                    {
                        JsonSerializer.Serialize(writer, fixedStrategy);
                    }

                    else if (value is BatchLongestStrategy)
                    {
                        writer.WriteStringValue(BatchLongestStrategy.VALUE);
                    }

                    else
                    {
                        throw new NotSupportedException($"Unsupported padding strategy type: {value.GetType()}");
                    }
                }
            }
        }

        public sealed class FixedStrategy(ulong maxLength): StrategyBase
        {
            // Yes, it is named "Fixed" in the JSON, not "fixed"
            [JsonPropertyName("Fixed")]
            public ulong MaxLength { get; set; } = maxLength;
        }

        public sealed class BatchLongestStrategy: StrategyBase
        {
            public const string VALUE = "BatchLongest";
        }

        public static readonly BatchLongestStrategy
            BATCH_LONGEST_STRATEGY = new(),
            DEFAULT_STRATEGY = BATCH_LONGEST_STRATEGY;

        [JsonPropertyName("strategy")]
        public StrategyBase? Strategy { get; set; } = strategy ?? DEFAULT_STRATEGY;

        [JsonPropertyName("direction")]
        public PaddingDirection Direction { get; set; } = direction;

        [JsonPropertyName("pad_to_multiple_of")]
        public ulong? PadToMultipleOf { get; set; } = padToMultipleOf;

        [JsonPropertyName("pad_id")]
        public uint PadID { get; set; } = padID;

        [JsonPropertyName("pad_type_id")]
        public uint PadTypeID { get; set; } = padTypeID;

        [JsonPropertyName("pad_token")]
        public string PadToken { get; set; } = padToken;
    }

    public sealed class AddedToken(
        uint id,
        string content,
        bool singleWord = false,
        bool leftStrip = false,
        bool rightStrip = false,
        bool normalized = true,
        bool special = false)
    {
        // https://docs.rs/tokenizers/latest/tokenizers/tokenizer/struct.AddedToken.html
        // For default values: https://huggingface.co/docs/tokenizers/v0.20.3/en/api/added-tokens#tokenizers.AddedToken

        // This field seem to have no effect, but include it for completeness.
        // ( I noticed that it is present in some tokenizer.json )
        [JsonPropertyName("id")]
        public uint ID { get; set; } = id;

        [JsonPropertyName("content")]
        public string Content { get; set; } = content;

        [JsonPropertyName("single_word")]
        public bool SingleWord { get; set; } = singleWord;

        [JsonPropertyName("lstrip")]
        public bool LeftStrip { get; set; } = leftStrip;

        [JsonPropertyName("rstrip")]
        public bool RightStrip { get; set; } = rightStrip;

        [JsonPropertyName("normalized")]
        public bool Normalized { get; set; } = normalized;

        [JsonPropertyName("special")]
        public bool Special { get; set; } = special;
    }
    
    public sealed class TokenizerData
    {
        [JsonPropertyName("truncation")]
        public Truncation? Truncation { get; set; }
        
        [JsonPropertyName("padding")]
        public Padding? Padding { get; set; }

        [JsonPropertyName("added_tokens")]
        public List<AddedToken> AddedTokens { get; set; }

        // [JsonPropertyName("normalizer")]
        // public Normalizer? Normalizer { get; set; }
        //
        // [JsonPropertyName("pre_tokenizer")]
        // public PreTokenizer? PreTokenizer { get; set; }
        //
        // [JsonPropertyName("post_processor")]
        // public PostProcessor? PostProcessor { get; set; }
        //
        // [JsonPropertyName("decoder")]
        // public Decoder? Decoder { get; set; }
        //
        // [JsonPropertyName("model")]
        // public Model? Model { get; set; }

        // This is to ensure we do not lose any additional fields
        // that are not explicitly defined in our TokenizerData structure.
        // This is paramount since we serialize the values back to JSON.
        [JsonInclude]
        [JsonExtensionData]
        private Dictionary<string, JsonElement> ExtensionData { get; set; }
    }

    // TODO: Implement these

    // public sealed class Normalizer
    // {
    //
    // }
    //
    // public sealed class PreTokenizer
    // {
    //
    // }
    //
    // public sealed class PostProcessor
    // {
    //
    // }
    //
    // public sealed class Decoder
    // {
    //
    // }
    //
    // public sealed class Model
    // {
    //
    // }
}