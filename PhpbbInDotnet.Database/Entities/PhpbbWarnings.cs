namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbWarnings
    {
        public int WarningId { get; set; }
        public int UserId { get; set; }
        public int PostId { get; set; }
        public int LogId { get; set; }
        public int WarningTime { get; set; }
    }
}
