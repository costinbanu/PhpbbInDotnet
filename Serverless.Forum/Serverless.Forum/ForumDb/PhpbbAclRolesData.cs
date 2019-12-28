using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbAclRolesData
    {
        public int RoleId { get; set; }
        public int AuthOptionId { get; set; }
        public byte AuthSetting { get; set; }
    }
}
