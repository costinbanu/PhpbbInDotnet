using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbBbcodes
    {
        public byte BbcodeId { get; set; }
        public string BbcodeTag { get; set; }
        public string BbcodeHelpline { get; set; }
        public byte DisplayOnPosting { get; set; }
        public string BbcodeMatch { get; set; }
        public string BbcodeTpl { get; set; }
        public string FirstPassMatch { get; set; }
        public string FirstPassReplace { get; set; }
        public string SecondPassMatch { get; set; }
        public string SecondPassReplace { get; set; }
    }
}
