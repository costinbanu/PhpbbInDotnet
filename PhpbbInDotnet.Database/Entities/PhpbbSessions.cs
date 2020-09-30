namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbSessions
    {
        public string SessionId { get; set; }
        public int SessionUserId { get; set; }
        public int SessionForumId { get; set; }
        public int SessionLastVisit { get; set; }
        public int SessionStart { get; set; }
        public int SessionTime { get; set; }
        public string SessionIp { get; set; }
        public string SessionBrowser { get; set; }
        public string SessionForwardedFor { get; set; }
        public string SessionPage { get; set; }
        public byte SessionViewonline { get; set; }
        public byte SessionAutologin { get; set; }
        public byte SessionAdmin { get; set; }
    }
}
