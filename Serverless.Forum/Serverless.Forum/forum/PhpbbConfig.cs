using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbConfig
    {
        public string ConfigName { get; set; }
        public string ConfigValue { get; set; }
        public byte IsDynamic { get; set; }
    }
}
