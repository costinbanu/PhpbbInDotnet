﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbAclGroups
    {
        public int GroupId { get; set; }
        public int ForumId { get; set; }
        public int AuthOptionId { get; set; }
        public int AuthRoleId { get; set; }
        public byte AuthSetting { get; set; }
    }
}
