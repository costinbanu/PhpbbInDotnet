﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbStylesImageset
    {
        public int ImagesetId { get; set; }
        public string ImagesetName { get; set; }
        public string ImagesetCopyright { get; set; }
        public string ImagesetPath { get; set; }
    }
}