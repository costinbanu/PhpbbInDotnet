using PhpbbInDotnet.Objects.Configuration;

namespace PhpbbInDotnet.RecurringTasks.UnitTests.Utils
{
    public class AppSettingsObject
    {
        public string? ForumDbConnectionString { get; set; }
        public Recaptcha? Recaptcha { get; set; }
        public Smtp? Smtp { get; set; }
        public Encryption? Encryption { get; set; }
        public string? AvatarSalt { get; set; }
        public Storage? Storage { get; set; }
        public string? BaseUrl { get; set; }
        public string? ForumName { get; set; }
        public string? LoginSessionSlidingExpiration { get; set; }
        public AttachmentLimits? UploadLimitsMB { get; set; }
        public AttachmentLimits? UploadLimitsCount { get; set; }
        public string? UserActivityTrackingInterval { get; set; }
        public string? AdminEmail { get; set; }
        public ExternalImageProcessor? ExternalImageProcessor { get; set; }
        public ImageSize? AvatarMaxSize { get; set; }
        public ImageSize? EmojiMaxSize { get; set; }
        public bool DisplayExternalLinksMenu { get; set; }
        public bool UseHeaderImage { get; set; }
        public string? RecycleBinRetentionTime { get; set; }
        public string? OperationLogsRetentionTime { get; set; }
        public string? InternetSearchUrlFormat { get; set; }
        public string? IpWhoIsUrlFormat { get; set; }
        public CleanupServiceOptions? CleanupService { get; set; }
    }

    public class Encryption
    {
        public string? Key1 { get; set; }
        public string? Key2 { get; set; }
    }

    public class Smtp
    {
        public string? Host { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool EnableSsl { get; set; }
        public int Port { get; set; }
    }
}
