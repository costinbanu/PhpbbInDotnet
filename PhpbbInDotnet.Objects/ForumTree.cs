using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class ForumTree
    {
        private string? _children;

        public int ForumId { get; set; }

        public ForumType? ForumType { get; set; }

        public string? ForumName { get; set; }

        public int? ParentId { get; set; }

        public bool IsRestricted { get; set; }

        public bool HasPassword { get; set; }

        public bool IsUnread { get; set; }
        
        public int Level { get; set; }

        public int? LeftId { get; set; }

        public string? ForumDesc { get; set; }

        public string? ForumDescUid { get; set; }
        
        public int? ForumLastPostId { get; set; }
        
        public int? ForumLastPosterId { get; set; }
        
        public string? ForumLastPostSubject { get; set; }
        
        public long? ForumLastPostTime { get; set; }
        
        public string? ForumLastPosterName { get; set; }
        
        public string? ForumLastPosterColour { get; set; }


        public string? Children
        {
            get => _children;
            set
            {
                _children = value;
                ChildrenList = StringUtility.ToIntHashSet(_children);
            }
        }

        public List<int>? PathList { get; set; }

        public HashSet<int>? ChildrenList { get; private set; }

        public int TotalTopicCount { get; set; }

        public int TotalSubforumCount { get; set; }

        public override bool Equals(object? obj)
            => obj is ForumTree tree && ForumId == tree?.ForumId;

        public override int GetHashCode()
            => HashCode.Combine(ForumId);
    }
}
