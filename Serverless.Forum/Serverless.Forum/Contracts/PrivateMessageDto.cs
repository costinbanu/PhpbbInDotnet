﻿using System;

namespace Serverless.Forum.Contracts
{
    public class PrivateMessageDto
    {
        public int OthersId { get; set; }
        public string OthersName { get; set; }
        public string OthersColor { get; set; }
        public string Subject { get; set; }
        public string Text { get; set; }
        public DateTime Time { get; set; }
        public byte Unread { get; set; }
        public int MessageId { get; set; }
    }
}
