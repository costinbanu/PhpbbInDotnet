using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbDisallow
    {
        public int DisallowId { get; set; }
        public string DisallowUsername { get; set; }
    }
}
