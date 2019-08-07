using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbRanks
    {
        public int RankId { get; set; }
        public string RankTitle { get; set; }
        public int RankMin { get; set; }
        public byte RankSpecial { get; set; }
        public string RankImage { get; set; }
    }
}
