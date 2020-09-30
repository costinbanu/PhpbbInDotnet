using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbBots
    {
        public int BotId { get; set; }
        public byte BotActive { get; set; }
        public string BotName { get; set; }
        public int UserId { get; set; }
        public string BotAgent { get; set; }
        public string BotIp { get; set; }
    }
}
