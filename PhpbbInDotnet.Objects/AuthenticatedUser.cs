﻿using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class AuthenticatedUser : AuthenticatedUserBase
    {
        public AuthenticatedUser(AuthenticatedUserBase @base)
        {
            UserId = @base.UserId;
            Username = @base.Username;
            UsernameClean = @base.UsernameClean;
            UserDateFormat = @base.UserDateFormat;
            UserColor = @base.UserColor;
            AllowPM = @base.AllowPM;
            JumpToUnread = @base.JumpToUnread;
            Language = @base.Language;
            EmailAddress = @base.EmailAddress;
        }

        public AuthenticatedUser() { }

        public HashSet<Permissions> AllPermissions { get; set; } = null;

        public Dictionary<int, int> TopicPostsPerPage { get; set; } = null;

        public int PostEditTime { get; set; } = 60;

        public int? UploadLimit { get; set; }

        public HashSet<int> Foes { get; set; }

        public string Style { get; set; }

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
