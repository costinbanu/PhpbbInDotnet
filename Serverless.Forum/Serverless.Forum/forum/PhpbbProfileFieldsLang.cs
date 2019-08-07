using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbProfileFieldsLang
    {
        public int FieldId { get; set; }
        public int LangId { get; set; }
        public int OptionId { get; set; }
        public byte FieldType { get; set; }
        public string LangValue { get; set; }
    }
}
