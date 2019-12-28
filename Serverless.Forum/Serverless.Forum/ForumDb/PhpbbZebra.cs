using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbZebra
    {
        public int UserId { get; set; }
        public int ZebraId { get; set; }
        public byte Friend { get; set; }
        public byte Foe { get; set; }
    }
}
