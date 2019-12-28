using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbAclUsers
    {
        public int UserId { get; set; }
        public int ForumId { get; set; }
        public int AuthOptionId { get; set; }
        public int AuthRoleId { get; set; }
        public byte AuthSetting { get; set; }
    }
}
