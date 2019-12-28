using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbWords
    {
        public int WordId { get; set; }
        public string Word { get; set; }
        public string Replacement { get; set; }
    }
}
