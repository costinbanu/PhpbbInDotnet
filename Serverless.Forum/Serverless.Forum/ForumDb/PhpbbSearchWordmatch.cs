﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbSearchWordmatch
    {
        public int PostId { get; set; }
        public int WordId { get; set; }
        public byte TitleMatch { get; set; }
        public long Id { get; set; }
    }
}