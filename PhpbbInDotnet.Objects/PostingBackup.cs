using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class PostingBackup
    {
        public PostingBackup(string? text, DateTime textTime, int forumId, int topicId, int postId, List<int>? attachmentIds)
        {
            Text = text;
            TextTime = textTime;
            ForumId = forumId;
            TopicId = topicId;
            PostId = postId;
            AttachmentIds = attachmentIds;
        }

        public string? Text { get; }
        public DateTime TextTime { get; }
        public int ForumId { get; }
        public int TopicId { get; }
        public int PostId { get; }
        public List<int>? AttachmentIds { get; }
    }
}
