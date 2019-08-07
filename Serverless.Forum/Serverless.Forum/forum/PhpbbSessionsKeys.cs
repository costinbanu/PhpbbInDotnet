using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbSessionsKeys
    {
        public string KeyId { get; set; }
        public int UserId { get; set; }
        public string LastIp { get; set; }
        public int LastLogin { get; set; }
    }
}
