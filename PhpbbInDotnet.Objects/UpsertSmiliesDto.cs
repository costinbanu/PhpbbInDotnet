using Microsoft.AspNetCore.Http;

namespace PhpbbInDotnet.Objects
{
    public class UpsertSmiliesDto
    {
        public string Url { get; set; }

        public string Emotion { get; set; }

        public IFormFile File { get; set; }

        public SmileyCode[] Codes { get; set; }

        public class SmileyCode
        {
            public int Id { get; set; }

            public string Value { get; set; }
        }
    }
}
