#nullable disable
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database.Entities;

namespace PhpbbInDotnet.Database.DbContexts
{
    class ForumDbContext : DbContext, IForumDbContext
    {
        public ForumDbContext(DbContextOptions options) : base(options) { }

        public virtual DbSet<PhpbbAclGroups> PhpbbAclGroups { get; set; }
        public virtual DbSet<PhpbbAclOptions> PhpbbAclOptions { get; set; }
        public virtual DbSet<PhpbbAclRoles> PhpbbAclRoles { get; set; }
        public virtual DbSet<PhpbbAclRolesData> PhpbbAclRolesData { get; set; }
        public virtual DbSet<PhpbbAclUsers> PhpbbAclUsers { get; set; }
        public virtual DbSet<PhpbbAttachments> PhpbbAttachments { get; set; }
        public virtual DbSet<PhpbbBanlist> PhpbbBanlist { get; set; }
        public virtual DbSet<PhpbbBbcodes> PhpbbBbcodes { get; set; }
        public virtual DbSet<PhpbbBots> PhpbbBots { get; set; }
        public virtual DbSet<PhpbbDrafts> PhpbbDrafts { get; set; }
        public virtual DbSet<PhpbbForums> PhpbbForums { get; set; }
        public virtual DbSet<PhpbbForumsTrack> PhpbbForumsTrack { get; set; }
        public virtual DbSet<PhpbbForumsWatch> PhpbbForumsWatch { get; set; }
        public virtual DbSet<PhpbbGroups> PhpbbGroups { get; set; }
        public virtual DbSet<PhpbbLang> PhpbbLang { get; set; }
        public virtual DbSet<PhpbbLog> PhpbbLog { get; set; }
        public virtual DbSet<PhpbbPollOptions> PhpbbPollOptions { get; set; }
        public virtual DbSet<PhpbbPollVotes> PhpbbPollVotes { get; set; }
        public virtual DbSet<PhpbbPosts> PhpbbPosts { get; set; }
        public virtual DbSet<PhpbbPrivmsgs> PhpbbPrivmsgs { get; set; }
        public virtual DbSet<PhpbbPrivmsgsTo> PhpbbPrivmsgsTo { get; set; }
        public virtual DbSet<PhpbbRanks> PhpbbRanks { get; set; }
        public virtual DbSet<PhpbbRecycleBin> PhpbbRecycleBin { get; set; }
        public virtual DbSet<PhpbbReports> PhpbbReports { get; set; }
        public virtual DbSet<PhpbbReportsReasons> PhpbbReportsReasons { get; set; }
        public virtual DbSet<PhpbbShortcuts> PhpbbShortcuts { get; set; }
        public virtual DbSet<PhpbbSmilies> PhpbbSmilies { get; set; }
        public virtual DbSet<PhpbbStyles> PhpbbStyles { get; set; }
        public virtual DbSet<PhpbbTopics> PhpbbTopics { get; set; }
        public virtual DbSet<PhpbbTopicsTrack> PhpbbTopicsTrack { get; set; }
        public virtual DbSet<PhpbbTopicsWatch> PhpbbTopicsWatch { get; set; }
        public virtual DbSet<PhpbbUserGroup> PhpbbUserGroup { get; set; }
        public virtual DbSet<PhpbbUserTopicPostNumber> PhpbbUserTopicPostNumber { get; set; }
        public virtual DbSet<PhpbbUsers> PhpbbUsers { get; set; }
        public virtual DbSet<PhpbbWords> PhpbbWords { get; set; }
        public virtual DbSet<PhpbbZebra> PhpbbZebra { get; set; }
    }
}
