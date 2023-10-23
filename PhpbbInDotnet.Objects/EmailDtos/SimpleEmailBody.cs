namespace PhpbbInDotnet.Objects.EmailDtos
{
    public class SimpleEmailBody
    {
        public SimpleEmailBody(string language, string userName)
        {
            Language = language;
            UserName = userName;
        }
        public string UserName { get; }
        public string Language { get; }
    }
}
