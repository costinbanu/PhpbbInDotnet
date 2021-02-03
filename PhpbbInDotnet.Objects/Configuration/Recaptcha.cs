namespace PhpbbInDotnet.Objects.Configuration
{
    public class Recaptcha
    {
        public string SiteKey { get; set; }

        public string SecretKey { get; set; }

        public string BaseAddress { get; set; }

        public string RelativeUri { get; set; }

        public string ClientName { get; set; }

        public decimal MinScore { get; set; }
    }
}
