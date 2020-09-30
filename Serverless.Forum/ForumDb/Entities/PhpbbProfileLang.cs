using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbProfileLang
    {
        public int FieldId { get; set; }
        public int LangId { get; set; }
        public string LangName { get; set; }
        public string LangExplain { get; set; }
        public string LangDefaultValue { get; set; }
    }
}
