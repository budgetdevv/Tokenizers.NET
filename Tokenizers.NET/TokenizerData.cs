using System.Text.Json.Serialization;

namespace Tokenizers.NET
{
    public struct Truncation
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
            
    public struct Padding
    {
        [JsonPropertyName("strategy")]
        public object? Strategy { get; set; }

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

    // public struct AddedTokens
    // {
    //             
    // }
    //         
    // public struct Normalizer
    // {
    //             
    // }
    //         
    // public struct PreTokenizer
    // {
    //             
    // }
    //         
    // public struct PostProcessor
    // {
    //             
    // }
    //         
    // public struct Decoder
    // {
    //             
    // }
    //         
    // public struct Model
    // {
    //             
    // }
    
    public struct TokenizerData
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
    }
}