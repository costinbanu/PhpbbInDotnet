using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class LoggedUser
    {
        public int UserId { get; set; }

        public string Username { get; set; } = null;

        public string UsernameClean { get; set; } = null;

        public HashSet<Permissions> AllPermissions { get; set; } = null;

        public Dictionary<int, int> TopicPostsPerPage { get; set; } = null;

        public string UserDateFormat { get; set; } = null;

        public string UserColor { get; set; } = null;

        public int PostEditTime { get; set; } = 60;

        public bool IsAnonymous => UserId == Constants.ANONYMOUS_USER_ID;

        public bool AllowPM { get; set; }

        public HashSet<int> Foes { get; set; }

        public string Style { get; set; }

        public byte JumpToUnread { get; set; } = 1;

        public int? UploadLimit { get; set; }

        public string Language { get; set; }

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
