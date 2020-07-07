using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbConfirm
    {
        public string ConfirmId { get; set; }
        public string SessionId { get; set; }
        public byte ConfirmType { get; set; }
        public string Code { get; set; }
        public int Seed { get; set; }
        public int Attempts { get; set; }
    }
}
