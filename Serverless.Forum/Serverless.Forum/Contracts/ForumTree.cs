﻿using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Contracts
{
    public class ForumTree
    {
        private string _path, _children, _topics, _unreadTopics, _unreadPosts;

        public int ForumId { get; set; }

        public int Level { get; set; }

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

        public string ForumChildren 
        {
            get => _children;
            set
            {
                _children = value;
                ChildrenList = _children.ToIntHashSet();
            }
        }

        public string Topics 
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
            => obj is ForumTree tree && ForumId == tree.ForumId;

        public override int GetHashCode()
            => HashCode.Combine(ForumId);
    }
}