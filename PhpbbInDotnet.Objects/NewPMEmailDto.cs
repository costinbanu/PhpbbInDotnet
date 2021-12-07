using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Objects
{
    public class NewPMEmailDto
    {
        public string? SenderName { get; set; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
    }
}