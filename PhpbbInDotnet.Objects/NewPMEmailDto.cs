namespace PhpbbInDotnet.Objects
{
    public class NewPMEmailDto
    {
        public NewPMEmailDto(string senderName, string language)
        {
            SenderName = senderName;
            Language = language;
        }


        public string SenderName { get; }
        public string Language { get; }
    }
}