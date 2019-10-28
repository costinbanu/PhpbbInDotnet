using System;
using System.Collections.Generic;

namespace Serverless.Forum.Contracts
{
    public class LoggedUser
    {
        public int? UserId { get; set; } = null;

        public string Username { get; set; } = null;

        public string UsernameClean { get; set; } = null;

        public IEnumerable<int> Groups { get; set; } = null;

        public IEnumerable<Permissions> UserPermissions { get; set; } = null;

        public Dictionary<int, int> TopicPostsPerPage { get; set; } = null;

        public string UserDateFormat { get; set; } = null;

        public string UserColor { get; set; } = null;

        //public IEnumerable<ForumAuthentication> ForumAuthentications { get; set; } = null;

        public class Permissions
        {
            public int ForumId { get; set; } = 0;

            public int AuthOptionId { get; set; } = 0;

            public int AuthRoleId { get; set; } = 0;

            public int AuthSetting { get; set; } = 0;
        }

        //public class ForumAuthentication
        //{
        //    public int ForumId { get; set; }

        //    public DateTime AcquiredAt { get; set; }
        //}
    }
}
