using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbQaConfirm
    {
        public string SessionId { get; set; }
        public string ConfirmId { get; set; }
        public string LangIso { get; set; }
        public int QuestionId { get; set; }
        public int Attempts { get; set; }
        public short ConfirmType { get; set; }
    }
}
