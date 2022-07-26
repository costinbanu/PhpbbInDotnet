namespace PhpbbInDotnet.Objects
{
    public class SimpleEmailBody
    {
        public SimpleEmailBody(string userName, string language)
        {
            UserName = userName;
            Language = language;
        }

        public string UserName { get; }

        public string Language { get; }
    }
}
