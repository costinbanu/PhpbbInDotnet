using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Contracts
{
    public class ForumTree
    {
        private string _path, _children, _topics;

        public int ForumId { get; set; }

        public ForumType? ForumType { get; set; }

        public string ForumName { get; set; }

        public string ForumDesc { get; set; }

        public string ForumDescUid { get; set; }

        public string ForumPassword { get; set; }

        public string PathToForum 
        {
            get => _path;
            set
            {
                _path = value;
                PathList = ParseList(_path);
            }
        }

        public string Children 
        {
            get => _children;
            set
            {
                _children = value;
                ChildList = ParseList(_children);
            }
        }

        public string Topics 
        {
            get => _topics;
            set
            {
                _topics = value;
                TopicList = ParseList(_topics);
            }
        }

        public HashSet<int> PathList { get; private set; }

        public HashSet<int> ChildList { get; private set; }

        public HashSet<int> TopicList { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is ForumTree tree)
            {
                return ForumId == tree.ForumId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ForumId);
        }

        private HashSet<int> ParseList (string list)
        {
            if (string.IsNullOrWhiteSpace(list))
            {
                return new HashSet<int>();
            }
            var items = list.Split(',');
            var toReturn = new HashSet<int>(items.Count());
            foreach (var item in items)
            {
                try
                {
                    if (int.TryParse(item.Trim(), out var val))
                    {
                        toReturn.Add(val);
                    }
                }
                catch { }
            };
            return toReturn;
        }
    }
}
