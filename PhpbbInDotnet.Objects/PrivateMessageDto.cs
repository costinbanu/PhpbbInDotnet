﻿using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain.Extensions;
using System;

namespace PhpbbInDotnet.Objects
{
    public class PrivateMessageDto : PaginatedResultSet
    {
        public int OthersId { get; set; }
        public string? OthersName { get; set; }
        public string? OthersColor { get; set; }
        public string? OthersAvatar { get; set; }
        public string? Subject { get; set; }
        public string? Text { get; set; }
        public long MessageTime { get; set; }
        public DateTime Time => MessageTime.ToUtcTime();
        public byte PmUnread { get; set; }
        public int MessageId { get; set; }
    }
}
