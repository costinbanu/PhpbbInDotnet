using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbAclGroups
    {
        public int GroupId { get; set; } = 0;
        public int ForumId { get; set; } = 0;
        public int AuthOptionId { get; set; } = 0;
        public int AuthRoleId { get; set; } = 0;
        public byte AuthSetting { get; set; } = 0;
    }
}
