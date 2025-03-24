using System.Text.Json.Serialization;

namespace PhpbbInDotnet.Objects.BotDetectorDtos
{
    public class SiteVerifyResponse
    {
        public bool Success { get; set; }
        
        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; set; } = [];
    }
}
