using Microsoft.AspNetCore.Http;

namespace PhpbbInDotnet.Objects
{
    public class UpsertSmiliesDto
    {
        public string Url { get; set; }

        public string Emotion { get; set; }

        public IFormFile File { get; set; }

        public string[] Codes { get; set; }
    }
}
