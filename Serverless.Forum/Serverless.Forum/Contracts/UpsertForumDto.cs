using Serverless.Forum.Utilities;
using System.Collections.Generic;

namespace Serverless.Forum.Contracts
{
    public class UpsertForumDto
    {
        public int? ForumId { get; set; }
        public string ForumName { get; set; }
        public string ForumDesc { get; set; }
        public bool? HasPassword { get; set; }
        public string ForumPassword { get; set; }
        public int? ParentId { get; set; }
        public ForumType? ForumType { get; set; }
        public List<int> ChildrenForums { get; set; }
        public List<string> UserForumPermissions { get; set; }
        public List<string> GroupForumPermissions { get; set; }
        public List<int> UserPermissionToRemove { get; set; }
        public List<int> GroupPermissionToRemove { get; set; }
    }
}
