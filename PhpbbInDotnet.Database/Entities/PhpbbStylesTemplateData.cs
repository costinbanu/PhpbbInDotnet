﻿namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbStylesTemplateData
    {
        public int TemplateId { get; set; }
        public string TemplateFilename { get; set; }
        public string TemplateIncluded { get; set; }
        public int TemplateMtime { get; set; }
        public string TemplateData { get; set; }
        public int Id { get; set; }
    }
}