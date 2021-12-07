namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbLang
    {
        public byte LangId { get; set; }
        public string LangIso { get; set; } = string.Empty;
        public string LangDir { get; set; } = string.Empty;
        public string LangEnglishName { get; set; } = string.Empty;
        public string LangLocalName { get; set; } = string.Empty;
        public string LangAuthor { get; set; } = string.Empty;
    }
}
