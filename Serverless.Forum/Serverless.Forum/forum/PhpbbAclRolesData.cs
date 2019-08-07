using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbAclRolesData
    {
        public int RoleId { get; set; }
        public int AuthOptionId { get; set; }
        public byte AuthSetting { get; set; }
    }
}
