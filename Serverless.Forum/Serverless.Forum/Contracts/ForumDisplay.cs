using Serverless.Forum.Pages;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Contracts
{
    public class ForumDisplay
    {
        public int? Id { get; set; } = null;

        public int? ParentId { get; set; } = null;

        public string Name { get; set; } = null;

        public string Description { get; set; } = null;

        public string DescriptionBbCodeUid { get; set; } = null;

        public string ForumPassword { get; set; } = null;

        public int? LastPosterId { get; set; } = null;

        public string LastPosterName { get; set; } = null;

        public DateTime? LastPostTime { get; set; } = null;

        public int? LastPostId { get; set; } = null;

        public IEnumerable<ForumDisplay> ChildrenForums { get; set; } = null;

        public IEnumerable<TopicDisplay> Topics { get; set; } = null;

        public bool Unread { get; set; } = false;

        public string LastPosterColor { get; set; } = null;

        public ForumType? ForumType { get; set; } = null;
    }
}
