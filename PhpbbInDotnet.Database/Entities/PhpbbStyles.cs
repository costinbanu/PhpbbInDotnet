﻿namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbStyles
    {
        public int StyleId { get; set; }
        public string StyleName { get; set; }
        public string StyleCopyright { get; set; }
        public byte StyleActive { get; set; }
        public int TemplateId { get; set; }
        public int ThemeId { get; set; }
        public int ImagesetId { get; set; }
    }
}