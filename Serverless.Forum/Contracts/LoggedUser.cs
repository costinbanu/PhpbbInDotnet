using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Serverless.Forum.Contracts
{
    public class LoggedUser
    {
        public int UserId { get; set; }

        public string Username { get; set; } = null;

        public string UsernameClean { get; set; } = null;

        public IEnumerable<int> Groups { get; set; } = null;

        public HashSet<Permissions> AllPermissions { get; set; } = null;

        public Dictionary<int, int> TopicPostsPerPage { get; set; } = null;

        public string UserDateFormat { get; set; } = null;

        public string UserColor { get; set; } = null;

        public int PostEditTime { get; set; } = 60;

        public bool IsAnonymous => UserId == Constants.ANONYMOUS_USER_ID;

        public class Permissions
        {
            public int ForumId { get; set; } = 0;

            public int AuthOptionId { get; set; } = 0;

            public int AuthRoleId { get; set; } = 0;

            public int AuthSetting { get; set; } = 0;

            public override bool Equals(object obj)
                => obj != null && obj is Permissions perm && ForumId == perm?.ForumId && AuthRoleId == perm?.AuthRoleId;

            public override int GetHashCode()
                => HashCode.Combine(ForumId, AuthRoleId);
        }
    }
}
