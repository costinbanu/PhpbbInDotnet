namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbProfileFieldsLang
    {
        public int FieldId { get; set; }
        public int LangId { get; set; }
        public int OptionId { get; set; }
        public byte FieldType { get; set; }
        public string LangValue { get; set; }
    }
}
