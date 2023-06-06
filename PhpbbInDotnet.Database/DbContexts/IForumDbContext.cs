using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PhpbbInDotnet.Database.Entities;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.DbContexts
{
    public interface IForumDbContext
    {
        DbSet<PhpbbAclGroups> PhpbbAclGroups { get; set; }
        DbSet<PhpbbAclOptions> PhpbbAclOptions { get; set; }
        DbSet<PhpbbAclRoles> PhpbbAclRoles { get; set; }
        DbSet<PhpbbAclRolesData> PhpbbAclRolesData { get; set; }
        DbSet<PhpbbAclUsers> PhpbbAclUsers { get; set; }
        DbSet<PhpbbAttachments> PhpbbAttachments { get; set; }
        DbSet<PhpbbBanlist> PhpbbBanlist { get; set; }
        DbSet<PhpbbBbcodes> PhpbbBbcodes { get; set; }
        DbSet<PhpbbBots> PhpbbBots { get; set; }
        DbSet<PhpbbDrafts> PhpbbDrafts { get; set; }
        DbSet<PhpbbForums> PhpbbForums { get; set; }
        DbSet<PhpbbForumsTrack> PhpbbForumsTrack { get; set; }
        DbSet<PhpbbForumsWatch> PhpbbForumsWatch { get; set; }
        DbSet<PhpbbGroups> PhpbbGroups { get; set; }
        DbSet<PhpbbLang> PhpbbLang { get; set; }
        DbSet<PhpbbLog> PhpbbLog { get; set; }
        DbSet<PhpbbPollOptions> PhpbbPollOptions { get; set; }
        DbSet<PhpbbPollVotes> PhpbbPollVotes { get; set; }
        DbSet<PhpbbPosts> PhpbbPosts { get; set; }
        DbSet<PhpbbPrivmsgs> PhpbbPrivmsgs { get; set; }
        DbSet<PhpbbPrivmsgsTo> PhpbbPrivmsgsTo { get; set; }
        DbSet<PhpbbRanks> PhpbbRanks { get; set; }
        DbSet<PhpbbRecycleBin> PhpbbRecycleBin { get; set; }
        DbSet<PhpbbReports> PhpbbReports { get; set; }
        DbSet<PhpbbReportsReasons> PhpbbReportsReasons { get; set; }
        DbSet<PhpbbShortcuts> PhpbbShortcuts { get; set; }
        DbSet<PhpbbSmilies> PhpbbSmilies { get; set; }
        DbSet<PhpbbStyles> PhpbbStyles { get; set; }
        DbSet<PhpbbTopics> PhpbbTopics { get; set; }
        DbSet<PhpbbTopicsTrack> PhpbbTopicsTrack { get; set; }
        DbSet<PhpbbTopicsWatch> PhpbbTopicsWatch { get; set; }
        DbSet<PhpbbUserGroup> PhpbbUserGroup { get; set; }
        DbSet<PhpbbUsers> PhpbbUsers { get; set; }
        DbSet<PhpbbUserTopicPostNumber> PhpbbUserTopicPostNumber { get; set; }
        DbSet<PhpbbWords> PhpbbWords { get; set; }
        DbSet<PhpbbZebra> PhpbbZebra { get; set; }

        DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);
    }
}