﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbCaptchaAnswers
    {
        public int QuestionId { get; set; }
        public string AnswerText { get; set; }
        public int Id { get; set; }
    }
}