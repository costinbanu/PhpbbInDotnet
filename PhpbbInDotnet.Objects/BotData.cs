using System;

namespace PhpbbInDotnet.Objects
{
    public class BotData
    {
        public string IP { get; set; } = string.Empty;

        public string UserAgent { get; set; } = string.Empty;

        public DateTime EntryTime { get; set; }

        public string SessionId { get; set; } = string.Empty;
    }
}
