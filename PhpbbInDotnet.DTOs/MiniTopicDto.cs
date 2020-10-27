using PhpbbInDotnet.Database.Entities;
using System.Web;

namespace PhpbbInDotnet.DTOs
{
    public class MiniTopicDto
    {
        public int ForumId { get; set; }

        public int TopicId { get; set; }

        public string TopicTitle { get; set; }
    }
}
