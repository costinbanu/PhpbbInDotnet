﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbIcons
    {
        public int IconsId { get; set; }
        public string IconsUrl { get; set; }
        public byte IconsWidth { get; set; }
        public byte IconsHeight { get; set; }
        public int IconsOrder { get; set; }
        public byte DisplayOnPosting { get; set; }
    }
}
