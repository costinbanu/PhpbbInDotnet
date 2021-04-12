using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Objects
{
    public class OperationLogSummary
    {
        public int UserId { get; set; }
        
        public string Username { get; set; }

        public int ForumId { get; set; }
        
        public string ForumName { get; set; }

        public int TopicId { get; set; }

        public string TopicTitle { get; set; }

        public OperationLogType LogType { get; set; }

        public string LogOperation { get; set; }

        public string LogData { get; set; }

        public long LogTime { get; set; }
    }
}
