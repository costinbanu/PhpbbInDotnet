using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Contracts
{
    public class ForumDto
    {
        private readonly ForumTree _node;
        private readonly Lazy<PhpbbForums> _nodeData;
        private readonly Lazy<IEnumerable<PhpbbForums>> _childrenForums;
        private readonly Lazy<IEnumerable<PhpbbTopics>> _topics;
        private readonly Lazy<bool> _unread;

        public ForumDto(ForumTree node, HashSet<ForumTree> tree, HashSet<PhpbbForums> forumData, HashSet<PhpbbTopics> topicData, HashSet<Tracking> tracking)
        {
            _node = node;
            _nodeData = new Lazy<PhpbbForums>(() => forumData.FirstOrDefault(f => f.ForumId == _node.ForumId));
            _childrenForums = new Lazy<IEnumerable<PhpbbForums>>(() => forumData.Where(t => t.ParentId == node.ForumId));
            _topics = new Lazy<IEnumerable<PhpbbTopics>>(() => (node.TopicsList?.Any() ?? false) ? topicData.Where(t => node.TopicsList?.Contains(t.TopicId) ?? false) : Enumerable.Empty<PhpbbTopics>());
            _unread = new Lazy<bool>(() => tracking.Any(t => t.ForumId == node.ForumId));
            Tree = tree;
            ForumData = forumData;
            TopicData = topicData;
            Tracking = tracking;
        }

        public int Id => _node.ForumId;

        public string Name => _nodeData.Value?.ForumName;

        public string Description => _nodeData.Value?.ForumDesc;

        public string DescriptionBbCodeUid => _nodeData.Value?.ForumDescUid;

        public string ForumPassword => _nodeData.Value?.ForumPassword;

        public int? LastPosterId => _nodeData.Value?.ForumLastPosterId;

        public string LastPosterName => _nodeData.Value?.ForumLastPosterName;

        public DateTime? LastPostTime => _nodeData.Value?.ForumLastPostTime.ToUtcTime();

        public int? LastPostId => _nodeData.Value?.ForumLastPostId;

        public IEnumerable<PhpbbForums> ChildrenForums => _childrenForums.Value;

        public IEnumerable<PhpbbTopics> Topics => _topics.Value;

        public bool Unread => _unread.Value;

        public string LastPosterColor => _nodeData.Value?.ForumLastPosterColour;

        public ForumType? ForumType => _nodeData.Value?.ForumType;

        public HashSet<Tracking> Tracking { get; }

        public HashSet<ForumTree> Tree { get; }

        public HashSet<PhpbbForums> ForumData { get; }

        public HashSet<PhpbbTopics> TopicData { get; }
    }
}
