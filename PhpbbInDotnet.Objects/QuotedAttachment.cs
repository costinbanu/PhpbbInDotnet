using System.Text.Json.Serialization;

namespace PhpbbInDotnet.Objects
{
    public class QuotedAttachment(int index, string? name)
    {
        [JsonPropertyName("index")]
        public int Index { get; } = index;

        [JsonPropertyName("name")]
        public string? Name { get; } = name;
    }
}
