namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbSmilies
    {
        public int SmileyId { get; set; }
        public string Code { get; set; }
        public string Emotion { get; set; }
        public string SmileyUrl { get; set; }
        public short SmileyWidth { get; set; }
        public short SmileyHeight { get; set; }
        public int SmileyOrder { get; set; }
        public byte DisplayOnPosting { get; set; }
    }
}
