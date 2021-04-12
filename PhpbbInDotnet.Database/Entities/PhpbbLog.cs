using PhpbbInDotnet.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbLog
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; } = 0;
        [Column(TypeName = "tinyint(2)")]
        public OperationLogType LogType { get; set; } = 0;
        public int UserId { get; set; } = 0;
        public int ForumId { get; set; } = 0;
        public int TopicId { get; set; } = 0;
        public int ReporteeId { get; set; } = 0;
        public string LogIp { get; set; } = string.Empty;
        public long LogTime { get; set; } = 0;
        public string LogOperation { get; set; } = string.Empty;
        public string LogData { get; set; } = string.Empty;
    }
}
