using PhpbbInDotnet.Utilities.Extensions;
using System;
using System.Text;
using System.Web;

namespace PhpbbInDotnet.Objects
{
    public class ReportDto
    {
        public int Id { get; set; }
        public string? ReasonTitle { get; set; }
        public string? ReasonDescription { get; set; }
        public string? Details { get; set; }
        public int ReporterId { get; set; }
        public string? ReporterUsername { get; set; }
        public int PostId { get; set; }
        public int ForumId { get; set; }
        public int TopicId { get; set; }
        public string? TopicTitle { get; set; }
        public long ReportTime { get; set; }
        public byte ReportClosed { get; set; }

        public DateTime ReportDateTime => ReportTime.ToUtcTime();

        public static ReportDto HtmlEncode(ReportDto initial)
            => new()
            {
                Id = initial.Id,
                ReasonTitle = MyHtmlEncode(initial.ReasonTitle),
                ReasonDescription = MyHtmlEncode(initial.ReasonDescription),
                Details = MyHtmlEncode(initial.Details),
                ReporterId = initial.ReporterId,
                ReporterUsername = MyHtmlEncode(initial.ReporterUsername),
                PostId = initial.PostId,
                ForumId = initial.ForumId,
                TopicId = initial.TopicId,
                TopicTitle = MyHtmlEncode(initial.TopicTitle),
                ReportTime = initial.ReportTime,
                ReportClosed = initial.ReportClosed
            };

        static string MyHtmlEncode(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            var chars = HttpUtility.HtmlEncode(value).ToCharArray();
            var encodedValue = new StringBuilder();
            foreach (char c in chars)
            {
                if (c > 127)
                { 
                    encodedValue.Append("&#" + (int)c + ";"); 
                }
                else
                { 
                    encodedValue.Append(c); 
                }
            }
            return encodedValue.ToString();
        }
    }
}
