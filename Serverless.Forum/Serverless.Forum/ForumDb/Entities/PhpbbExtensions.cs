﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbExtensions
    {
        public int ExtensionId { get; set; }
        public int GroupId { get; set; }
        public string Extension { get; set; }
    }
}