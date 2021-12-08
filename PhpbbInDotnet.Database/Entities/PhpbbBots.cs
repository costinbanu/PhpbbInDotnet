namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbBots
    {
        public int BotId { get; set; }
        public byte BotActive { get; set; }
        public string BotName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string BotAgent { get; set; } = string.Empty;
        public string BotIp { get; set; } = string.Empty;
    }
}
