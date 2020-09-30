using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbReports
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReportId { get; set; } = 0;
        public short ReasonId { get; set; } = 0;
        public int PostId { get; set; } = 0;
        public int PmId { get; set; } = 0;
        public int UserId { get; set; } = 0;
        public byte UserNotify { get; set; } = 0;
        public byte ReportClosed { get; set; } = 0;
        public long ReportTime { get; set; } = 0;
        public string ReportText { get; set; } = string.Empty;
    }
}
