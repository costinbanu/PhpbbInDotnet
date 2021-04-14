namespace PhpbbInDotnet.Objects
{
    public class UpsertBanListDto
    {
        public int BanId { get; set; } = 0;
        public string BanIp { get; set; } = string.Empty;
        public string BanIpOldValue { get; set; } = string.Empty;
        public string BanEmail { get; set; } = string.Empty;
        public string BanEmailOldValue { get; set; } = string.Empty;
    }
}
