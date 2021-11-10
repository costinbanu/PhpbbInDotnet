namespace PhpbbInDotnet.Objects.Configuration
{
    public class ExternalImageProcessorApi : ApiHttpClientOptions
    {
        public bool Enabled { get; set; }

        public string ApiKey { get; set; }
    }
}
