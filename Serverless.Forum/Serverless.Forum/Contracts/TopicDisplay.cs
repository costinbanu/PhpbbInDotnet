using System;

namespace Serverless.Forum.Pages
{
    public class TopicDisplay
    {
        public int? Id { get; set; } = null;

        public string Title { get; set; } = null;

        public string LastPosterName { get; set; } = null;

        public DateTime? LastPostTime { get; set; } = null;
    }
}