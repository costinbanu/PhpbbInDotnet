using PhpbbInDotnet.Forum.ForumDb.Entities;
using System.Web;

namespace PhpbbInDotnet.Forum.Contracts
{
    public class MiniTopicDto
    {
        public int ForumId { get; set; }

        public int TopicId { get; set; }

        public string TopicTitle { get; set; }

        public MiniTopicDto(PhpbbTopics phpbbTopics)
        {
            ForumId = phpbbTopics.ForumId;
            TopicId = phpbbTopics.TopicId;
            TopicTitle = HttpUtility.HtmlDecode(phpbbTopics.TopicTitle);
        }

        public MiniTopicDto() { }
    }
}
