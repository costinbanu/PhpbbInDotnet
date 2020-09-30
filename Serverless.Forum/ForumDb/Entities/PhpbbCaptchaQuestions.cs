using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbCaptchaQuestions
    {
        public int QuestionId { get; set; }
        public byte Strict { get; set; }
        public int LangId { get; set; }
        public string LangIso { get; set; }
        public string QuestionText { get; set; }
    }
}
