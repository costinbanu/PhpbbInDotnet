namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbReportsReasons
    {
        public short ReasonId { get; set; }
        public string ReasonTitle { get; set; } = string.Empty;
        public string ReasonDescription { get; set; } = string.Empty;
        public short ReasonOrder { get; set; }
    }
}
