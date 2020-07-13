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

        public static bool operator == (LoggedUser left, LoggedUser right) => left?.UserId == right?.UserId;

        public static bool operator != (LoggedUser left, LoggedUser right) => !(left == right);

        public override bool Equals(object obj) => obj is LoggedUser other && other == this;

        public override int GetHashCode() => UserId.GetHashCode();

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

        public class PermissionsByForumIdComparer : IEqualityComparer<Permissions>
        {
            public bool Equals([AllowNull] Permissions x, [AllowNull] Permissions y)
                => x != null && y != null && x.ForumId == y.ForumId;

            public int GetHashCode([DisallowNull] Permissions obj)
                => obj.ForumId.GetHashCode();
        }
    }
}
