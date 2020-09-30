namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbSearchWordmatch
    {
        public int PostId { get; set; }
        public int WordId { get; set; }
        public byte TitleMatch { get; set; }
        public long Id { get; set; }
    }
}
