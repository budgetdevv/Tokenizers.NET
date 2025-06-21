using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tokenizers.NET
{
    public sealed class Truncation
    {
        [JsonPropertyName("direction")]
        public string Direction { get; set; }
        
        [JsonPropertyName("max_length")]
        public int MaxLength { get; set; }
        
        [JsonPropertyName("strategy")]
        public string Strategy { get; set; }
        
        [JsonPropertyName("stride")]
        public int Stride { get; set; }
    }
            
    public sealed class Padding
    {
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

        public sealed class FixedStrategy(int length): StrategyBase
        {
            // Yes, it is named "Fixed" in the JSON, not "fixed"
            [JsonPropertyName("Fixed")]
            public int Length { get; set; } = length;
        }

        public sealed class BatchLongestStrategy: StrategyBase
        {
            public const string VALUE = "BatchLongest";
        }

        [JsonPropertyName("strategy")]
        public StrategyBase? Strategy { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("max_length")]
        public int? MaxLength { get; set; }

        [JsonPropertyName("pad_id")]
        public int PadID { get; set; }

        [JsonPropertyName("pad_token")]
        public string PadToken { get; set; }

        [JsonPropertyName("pad_type_id")]
        public int PadTypeID { get; set; }

        [JsonPropertyName("pad_to_multiple_of")]
        public int? PadToMultipleOf { get; set; }
    }

    // public sealed class AddedTokens
    // {
    //
    // }
    //
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
    
    public sealed class TokenizerData
    {
        [JsonPropertyName("truncation")]
        public Truncation? Truncation { get; set; }
        
        [JsonPropertyName("padding")]
        public Padding? Padding { get; set; }

        // [JsonPropertyName("added_tokens")]
        // public AddedTokens? AddedTokens { get; init; }
        //
        // [JsonPropertyName("normalizer")]
        // public Normalizer? Normalizer { get; init; }
        //
        // [JsonPropertyName("pre_tokenizer")]
        // public PreTokenizer? PreTokenizer { get; init; }
        //
        // [JsonPropertyName("post_processor")]
        // public PostProcessor? PostProcessor { get; init; }
        //
        // [JsonPropertyName("decoder")]
        // public Decoder? Decoder { get; init; }
        //
        // [JsonPropertyName("model")]
        // public Model? Model { get; init; }

        [JsonInclude]
        [JsonExtensionData]
        private Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}