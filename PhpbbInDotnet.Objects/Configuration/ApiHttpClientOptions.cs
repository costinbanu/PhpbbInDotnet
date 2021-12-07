using System.Collections.Generic;

namespace PhpbbInDotnet.Objects.Configuration
{
    public abstract class ApiHttpClientOptions
    {
        public string? BaseAddress { get; set; }

        public string? RelativeUri { get; set; }

        public string? ClientName { get; set; }
    }
}
