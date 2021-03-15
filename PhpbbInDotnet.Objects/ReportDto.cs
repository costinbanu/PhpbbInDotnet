namespace PhpbbInDotnet.Objects
{
    public class ReportDto
    {
        public int Id { get; set; }
        public string ReasonTitle { get; set; }
        public string ReasonDescription { get; set; }
        public string Details { get; set; }
        public int ReporterId { get; set; }
        public string ReporterUsername { get; set; }
        public int PostId { get; set; }
    }
}
