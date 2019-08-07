using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbReports
    {
        public int ReportId { get; set; }
        public short ReasonId { get; set; }
        public int PostId { get; set; }
        public int PmId { get; set; }
        public int UserId { get; set; }
        public byte UserNotify { get; set; }
        public byte ReportClosed { get; set; }
        public int ReportTime { get; set; }
        public string ReportText { get; set; }
    }
}
