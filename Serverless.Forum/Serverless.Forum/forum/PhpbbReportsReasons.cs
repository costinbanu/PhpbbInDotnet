using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbReportsReasons
    {
        public short ReasonId { get; set; }
        public string ReasonTitle { get; set; }
        public string ReasonDescription { get; set; }
        public short ReasonOrder { get; set; }
    }
}
