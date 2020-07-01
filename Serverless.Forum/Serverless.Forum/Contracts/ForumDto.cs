using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Contracts
{
    public class ForumDto
    {
        private readonly Lazy<IEnumerable<PhpbbForums>> _childrenForums;

        public ForumDto(ForumTree node, HashSet<ForumTree> tree, HashSet<PhpbbForums> forumData, HashSet<PhpbbTopics> topicData, HashSet<Tracking> tracking)
        {
            _childrenForums = new Lazy<IEnumerable<PhpbbForums>>(() => forumData.Where(t => t.ParentId == node.ForumId));
            Tree = tree;
            ForumData = forumData;
            TopicData = topicData;
            Tracking = tracking;
        }

        public IEnumerable<PhpbbForums> ChildrenForums => _childrenForums.Value;

        public HashSet<Tracking> Tracking { get; }

        public HashSet<ForumTree> Tree { get; }

        public HashSet<PhpbbForums> ForumData { get; }

        public HashSet<PhpbbTopics> TopicData { get; }
    }
}
