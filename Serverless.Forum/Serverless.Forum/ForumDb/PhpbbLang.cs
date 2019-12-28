using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbLang
    {
        public byte LangId { get; set; }
        public string LangIso { get; set; }
        public string LangDir { get; set; }
        public string LangEnglishName { get; set; }
        public string LangLocalName { get; set; }
        public string LangAuthor { get; set; }
    }
}
