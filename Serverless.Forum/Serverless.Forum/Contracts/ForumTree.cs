using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Serverless.Forum.Contracts
{
    public class ForumTree
    {
        private string _path, _children, _topics;

        public int ForumId { get; set; }

        public ForumType ForumType { get; set; }

        public string ForumName { get; set; }

        public int ParentId { get; set; }

        public string PathToForum
        {
            get => _path;
            set
            {
                _path = value;
                PathList = _path.ToIntHashSet();
            }
        }

        public bool IsRestricted { get; set; }

        public bool HasPassword { get; set; }
        
        public int Level { get; set; }

        public int LeftId { get; set; }

        public string ForumDesc { get; set; }

        public string ForumDescUid { get; set; }
        
        public int ForumLastPostId { get; set; }
        
        public int ForumLastPosterId { get; set; }
        
        public string ForumLastPostSubject { get; set; }
        
        public long ForumLastPostTime { get; set; }
        
        public string ForumLastPosterName { get; set; }
        
        public string ForumLastPosterColour { get; set; }


        public string ChildList 
        {
            get => _children;
            set
            {
                _children = value;
                ChildrenList = _children.ToIntHashSet();
            }
        }

        public string TopicList 
        {
            get => _topics;
            set
            {
                _topics = value;
                TopicsList = _topics.ToIntHashSet();
            }
        }

        public HashSet<int> PathList { get; private set; }

        public HashSet<int> ChildrenList { get; private set; }

        public HashSet<int> TopicsList { get; private set; }

        public override bool Equals(object obj)
            => obj != null && obj is ForumTree tree && ForumId == tree?.ForumId;

        public override int GetHashCode()
            => HashCode.Combine(ForumId);
    }
}
