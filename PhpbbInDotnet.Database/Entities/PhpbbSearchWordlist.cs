namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbSearchWordlist
    {
        public int WordId { get; set; }
        public string WordText { get; set; }
        public byte WordCommon { get; set; }
        public int WordCount { get; set; }
    }
}
