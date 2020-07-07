using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbStylesTemplate
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string TemplateCopyright { get; set; }
        public string TemplatePath { get; set; }
        public string BbcodeBitfield { get; set; }
        public byte TemplateStoredb { get; set; }
        public int TemplateInheritsId { get; set; }
        public string TemplateInheritPath { get; set; }
    }
}
