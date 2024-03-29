﻿namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbAclOptions
    {
        public int AuthOptionId { get; set; }
        public string AuthOption { get; set; } = string.Empty;
        public byte IsGlobal { get; set; }
        public byte IsLocal { get; set; }
        public byte FounderOnly { get; set; }
    }
}
