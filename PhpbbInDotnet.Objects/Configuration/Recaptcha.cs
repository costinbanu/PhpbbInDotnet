namespace PhpbbInDotnet.Objects.Configuration
{
    public class Recaptcha : ApiHttpClientOptions
    {
        public string? SiteKey { get; set; }

        public string? SecretKey { get; set; }

        public decimal MinScore { get; set; }
    }
}
