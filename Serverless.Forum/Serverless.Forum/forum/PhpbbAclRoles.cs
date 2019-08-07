using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbAclRoles
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string RoleDescription { get; set; }
        public string RoleType { get; set; }
        public short RoleOrder { get; set; }
    }
}
