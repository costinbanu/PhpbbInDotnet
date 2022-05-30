using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database.Entities;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database
{
    class ForumDbContext : DbContext, IForumDbContext
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public ForumDbContext(DbContextOptions<ForumDbContext> options) : base(options) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        #region Entities

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

        #endregion Entities

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "2.2.6-servicing-10079");

            modelBuilder.Entity<PhpbbAclGroups>(entity =>
            {
                entity.HasKey(e => new { e.GroupId, e.ForumId, e.AuthOptionId, e.AuthRoleId });

                entity.ToTable("phpbb_acl_groups", "forum");

                entity.HasIndex(e => e.AuthOptionId)
                    .HasDatabaseName("auth_opt_id");

                entity.HasIndex(e => e.AuthRoleId)
                    .HasDatabaseName("auth_role_id");

                entity.HasIndex(e => e.GroupId)
                    .HasDatabaseName("group_id");

                entity.Property(e => e.GroupId)
                    .HasColumnName("group_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthOptionId)
                    .HasColumnName("auth_option_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthRoleId)
                    .HasColumnName("auth_role_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthSetting)
                    .HasColumnName("auth_setting")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbAclOptions>(entity =>
            {
                entity.HasKey(e => e.AuthOptionId);

                entity.ToTable("phpbb_acl_options", "forum");

                entity.HasIndex(e => e.AuthOption)
                    .HasDatabaseName("auth_option")
                    .IsUnique();

                entity.Property(e => e.AuthOptionId)
                    .HasColumnName("auth_option_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.AuthOption)
                    .IsRequired()
                    .HasColumnName("auth_option")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.FounderOnly)
                    .HasColumnName("founder_only")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.IsGlobal)
                    .HasColumnName("is_global")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.IsLocal)
                    .HasColumnName("is_local")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbAclRoles>(entity =>
            {
                entity.HasKey(e => e.RoleId);

                entity.ToTable("phpbb_acl_roles", "forum");

                entity.HasIndex(e => e.RoleOrder)
                    .HasDatabaseName("role_order");

                entity.HasIndex(e => e.RoleType)
                    .HasDatabaseName("role_type");

                entity.Property(e => e.RoleId)
                    .HasColumnName("role_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.RoleDescription)
                    .IsRequired()
                    .HasColumnName("role_description")
                    .IsUnicode(false);

                entity.Property(e => e.RoleName)
                    .IsRequired()
                    .HasColumnName("role_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.RoleOrder)
                    .HasColumnName("role_order")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.RoleType)
                    .IsRequired()
                    .HasColumnName("role_type")
                    .HasMaxLength(10)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<PhpbbAclRolesData>(entity =>
            {
                entity.HasKey(e => new { e.RoleId, e.AuthOptionId });

                entity.ToTable("phpbb_acl_roles_data", "forum");

                entity.HasIndex(e => e.AuthOptionId)
                    .HasDatabaseName("ath_op_id");

                entity.Property(e => e.RoleId)
                    .HasColumnName("role_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthOptionId)
                    .HasColumnName("auth_option_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthSetting)
                    .HasColumnName("auth_setting")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbAclUsers>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ForumId, e.AuthOptionId, e.AuthRoleId });

                entity.ToTable("phpbb_acl_users", "forum");

                entity.HasIndex(e => e.AuthOptionId)
                    .HasDatabaseName("auth_option_id");

                entity.HasIndex(e => e.AuthRoleId)
                    .HasDatabaseName("auth_role_id");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("user_id");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthOptionId)
                    .HasColumnName("auth_option_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthRoleId)
                    .HasColumnName("auth_role_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthSetting)
                    .HasColumnName("auth_setting")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbAttachments>(entity =>
            {
                entity.HasKey(e => e.AttachId);

                entity.ToTable("phpbb_attachments", "forum");

                entity.HasIndex(e => e.Filetime)
                    .HasDatabaseName("filetime");

                entity.HasIndex(e => e.IsOrphan)
                    .HasDatabaseName("is_orphan");

                entity.HasIndex(e => e.PostMsgId)
                    .HasDatabaseName("post_msg_id");

                entity.HasIndex(e => e.PosterId)
                    .HasDatabaseName("poster_id");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.Property(e => e.AttachId)
                    .HasColumnName("attach_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.AttachComment)
                    .IsRequired()
                    .HasColumnName("attach_comment")
                    .IsUnicode(false);

                entity.Property(e => e.DownloadCount)
                    .HasColumnName("download_count")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.Extension)
                    .IsRequired()
                    .HasColumnName("extension")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.Filesize)
                    .HasColumnName("filesize")
                    .HasColumnType("int(20) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.Filetime)
                    .HasColumnName("filetime")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.InMessage)
                    .HasColumnName("in_message")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.IsOrphan)
                    .HasColumnName("is_orphan")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.Mimetype)
                    .IsRequired()
                    .HasColumnName("mimetype")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.PhysicalFilename)
                    .IsRequired()
                    .HasColumnName("physical_filename")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.PostMsgId)
                    .HasColumnName("post_msg_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PosterId)
                    .HasColumnName("poster_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.RealFilename)
                    .IsRequired()
                    .HasColumnName("real_filename")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Thumbnail)
                    .HasColumnName("thumbnail")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbBanlist>(entity =>
            {
                entity.HasKey(e => e.BanId);

                entity.ToTable("phpbb_banlist", "forum");

                entity.HasIndex(e => e.BanEnd)
                    .HasDatabaseName("ban_end");

                entity.HasIndex(e => new { e.BanEmail, e.BanExclude })
                    .HasDatabaseName("ban_email");

                entity.HasIndex(e => new { e.BanIp, e.BanExclude })
                    .HasDatabaseName("ban_ip");

                entity.HasIndex(e => new { e.BanUserid, e.BanExclude })
                    .HasDatabaseName("ban_user");

                entity.Property(e => e.BanId)
                    .HasColumnName("ban_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.BanEmail)
                    .IsRequired()
                    .HasColumnName("ban_email")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.BanEnd)
                    .HasColumnName("ban_end")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.BanExclude)
                    .HasColumnName("ban_exclude")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.BanGiveReason)
                    .IsRequired()
                    .HasColumnName("ban_give_reason")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BanIp)
                    .IsRequired()
                    .HasColumnName("ban_ip")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.BanReason)
                    .IsRequired()
                    .HasColumnName("ban_reason")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BanStart)
                    .HasColumnName("ban_start")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.BanUserid)
                    .HasColumnName("ban_userid")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbBbcodes>(entity =>
            {
                entity.HasKey(e => e.BbcodeId);

                entity.ToTable("phpbb_bbcodes", "forum");

                entity.HasIndex(e => e.DisplayOnPosting)
                    .HasDatabaseName("display_on_post");

                entity.Property(e => e.BbcodeId)
                    .HasColumnName("bbcode_id")
                    .HasColumnType("tinyint(3)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.BbcodeHelpline)
                    .IsRequired()
                    .HasColumnName("bbcode_helpline")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BbcodeMatch)
                    .IsRequired()
                    .HasColumnName("bbcode_match")
                    .IsUnicode(false);

                entity.Property(e => e.BbcodeTag)
                    .IsRequired()
                    .HasColumnName("bbcode_tag")
                    .HasMaxLength(16)
                    .IsUnicode(false);

                entity.Property(e => e.BbcodeTpl)
                    .IsRequired()
                    .HasColumnName("bbcode_tpl")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.DisplayOnPosting)
                    .HasColumnName("display_on_posting")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.FirstPassMatch)
                    .IsRequired()
                    .HasColumnName("first_pass_match")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.FirstPassReplace)
                    .IsRequired()
                    .HasColumnName("first_pass_replace")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.SecondPassMatch)
                    .IsRequired()
                    .HasColumnName("second_pass_match")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.SecondPassReplace)
                    .IsRequired()
                    .HasColumnName("second_pass_replace")
                    .HasColumnType("mediumtext");
            });

            modelBuilder.Entity<PhpbbBots>(entity =>
            {
                entity.HasKey(e => e.BotId);

                entity.ToTable("phpbb_bots", "forum");

                entity.HasIndex(e => e.BotActive)
                    .HasDatabaseName("bot_active");

                entity.Property(e => e.BotId)
                    .HasColumnName("bot_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.BotActive)
                    .HasColumnName("bot_active")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.BotAgent)
                    .IsRequired()
                    .HasColumnName("bot_agent")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BotIp)
                    .IsRequired()
                    .HasColumnName("bot_ip")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BotName)
                    .IsRequired()
                    .HasColumnName("bot_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbDrafts>(entity =>
            {
                entity.HasKey(e => e.DraftId);

                entity.ToTable("phpbb_drafts", "forum");

                entity.HasIndex(e => e.SaveTime)
                    .HasDatabaseName("save_time");

                entity.Property(e => e.DraftId)
                    .HasColumnName("draft_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.DraftMessage)
                    .IsRequired()
                    .HasColumnName("draft_message")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.DraftSubject)
                    .IsRequired()
                    .HasColumnName("draft_subject")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.SaveTime)
                    .HasColumnName("save_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbForums>(entity =>
            {
                entity.HasKey(e => e.ForumId);

                entity.ToTable("phpbb_forums", "forum");

                entity.HasIndex(e => e.ForumLastPostId)
                    .HasDatabaseName("forum_lastpost_id");

                entity.HasIndex(e => new { e.LeftId, e.RightId })
                    .HasDatabaseName("left_right_id");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.DisplayOnIndex)
                    .HasColumnName("display_on_index")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.DisplaySubforumList)
                    .HasColumnName("display_subforum_list")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableIcons)
                    .HasColumnName("enable_icons")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableIndexing)
                    .HasColumnName("enable_indexing")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnablePrune)
                    .HasColumnName("enable_prune")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumDesc)
                    .IsRequired()
                    .HasColumnName("forum_desc")
                    .IsUnicode(false);

                entity.Property(e => e.ForumDescBitfield)
                    .IsRequired()
                    .HasColumnName("forum_desc_bitfield")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumDescOptions)
                    .HasColumnName("forum_desc_options")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("7");

                entity.Property(e => e.ForumDescUid)
                    .IsRequired()
                    .HasColumnName("forum_desc_uid")
                    .HasMaxLength(8)
                    .IsUnicode(false);

                entity.Property(e => e.ForumEditTime)
                    .HasColumnName("forum_edit_time")
                    .HasColumnType("int(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumFlags)
                    .HasColumnName("forum_flags")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("32");

                entity.Property(e => e.ForumImage)
                    .IsRequired()
                    .HasColumnName("forum_image")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumLastPostId)
                    .HasColumnName("forum_last_post_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumLastPostSubject)
                    .IsRequired()
                    .HasColumnName("forum_last_post_subject")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumLastPostTime)
                    .HasColumnName("forum_last_post_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumLastPosterColour)
                    .IsRequired()
                    .HasColumnName("forum_last_poster_colour")
                    .HasMaxLength(6)
                    .IsUnicode(false);

                entity.Property(e => e.ForumLastPosterId)
                    .HasColumnName("forum_last_poster_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumLastPosterName)
                    .IsRequired()
                    .HasColumnName("forum_last_poster_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumLink)
                    .IsRequired()
                    .HasColumnName("forum_link")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumName)
                    .IsRequired()
                    .HasColumnName("forum_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumOptions)
                    .HasColumnName("forum_options")
                    .HasColumnType("int(20) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumParents)
                    .IsRequired()
                    .HasColumnName("forum_parents")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.ForumPassword)
                    .IsRequired()
                    .HasColumnName("forum_password")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.ForumPosts)
                    .HasColumnName("forum_posts")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumRules)
                    .IsRequired()
                    .HasColumnName("forum_rules")
                    .IsUnicode(false);

                entity.Property(e => e.ForumRulesBitfield)
                    .IsRequired()
                    .HasColumnName("forum_rules_bitfield")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumRulesLink)
                    .IsRequired()
                    .HasColumnName("forum_rules_link")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ForumRulesOptions)
                    .HasColumnName("forum_rules_options")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("7");

                entity.Property(e => e.ForumRulesUid)
                    .IsRequired()
                    .HasColumnName("forum_rules_uid")
                    .HasMaxLength(8)
                    .IsUnicode(false);

                entity.Property(e => e.ForumStatus)
                    .HasColumnName("forum_status")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumStyle)
                    .HasColumnName("forum_style")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumTopics)
                    .HasColumnName("forum_topics")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumTopicsPerPage)
                    .HasColumnName("forum_topics_per_page")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumTopicsReal)
                    .HasColumnName("forum_topics_real")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumType)
                    .HasColumnName("forum_type")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.LeftId)
                    .HasColumnName("left_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ParentId)
                    .HasColumnName("parent_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PruneDays)
                    .HasColumnName("prune_days")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PruneFreq)
                    .HasColumnName("prune_freq")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PruneNext)
                    .HasColumnName("prune_next")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PruneViewed)
                    .HasColumnName("prune_viewed")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.RightId)
                    .HasColumnName("right_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });


            modelBuilder.Entity<PhpbbForumsTrack>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ForumId });

                entity.ToTable("phpbb_forums_track", "forum");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MarkTime)
                    .HasColumnName("mark_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbForumsWatch>(entity =>
            {
                entity.HasKey(e => new { e.ForumId, e.UserId });

                entity.ToTable("phpbb_forums_watch", "forum");

                entity.HasIndex(e => e.ForumId)
                    .HasDatabaseName("forum_id");

                entity.HasIndex(e => e.NotifyStatus)
                    .HasDatabaseName("notify_stat");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("user_id");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.NotifyStatus)
                    .HasColumnName("notify_status")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbGroups>(entity =>
            {
                entity.HasKey(e => e.GroupId);

                entity.ToTable("phpbb_groups", "forum");

                entity.HasIndex(e => new { e.GroupLegend, e.GroupName })
                    .HasDatabaseName("group_legend_name");

                entity.Property(e => e.GroupId)
                    .HasColumnName("group_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.GroupAvatar)
                    .IsRequired()
                    .HasColumnName("group_avatar")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.GroupAvatarHeight)
                    .HasColumnName("group_avatar_height")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupAvatarType)
                    .HasColumnName("group_avatar_type")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupAvatarWidth)
                    .HasColumnName("group_avatar_width")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupColour)
                    .IsRequired()
                    .HasColumnName("group_colour")
                    .HasMaxLength(6)
                    .IsUnicode(false);

                entity.Property(e => e.GroupDesc)
                    .IsRequired()
                    .HasColumnName("group_desc")
                    .IsUnicode(false);

                entity.Property(e => e.GroupDescBitfield)
                    .IsRequired()
                    .HasColumnName("group_desc_bitfield")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.GroupDescOptions)
                    .HasColumnName("group_desc_options")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("7");

                entity.Property(e => e.GroupDescUid)
                    .IsRequired()
                    .HasColumnName("group_desc_uid")
                    .HasMaxLength(8)
                    .IsUnicode(false);

                entity.Property(e => e.GroupDisplay)
                    .HasColumnName("group_display")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupFounderManage)
                    .HasColumnName("group_founder_manage")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupLegend)
                    .HasColumnName("group_legend")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.GroupMaxRecipients)
                    .HasColumnName("group_max_recipients")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupMessageLimit)
                    .HasColumnName("group_message_limit")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupName)
                    .IsRequired()
                    .HasColumnName("group_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.GroupRank)
                    .HasColumnName("group_rank")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupReceivePm)
                    .HasColumnName("group_receive_pm")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupSigChars)
                    .HasColumnName("group_sig_chars")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupSkipAuth)
                    .HasColumnName("group_skip_auth")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupType)
                    .HasColumnName("group_type")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.GroupUserUploadSize)
                    .HasColumnName("group_user_upload_size")
                    .HasColumnType("int(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupEditTime)
                    .HasColumnName("group_edit_time")
                    .HasColumnType("int(4) unsigned")
                    .HasDefaultValueSql("60");
            });

            modelBuilder.Entity<PhpbbLang>(entity =>
            {
                entity.HasKey(e => e.LangId);

                entity.ToTable("phpbb_lang", "forum");

                entity.HasIndex(e => e.LangIso)
                    .HasDatabaseName("lang_iso");

                entity.Property(e => e.LangId)
                    .HasColumnName("lang_id")
                    .HasColumnType("tinyint(4)")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.LangAuthor)
                    .IsRequired()
                    .HasColumnName("lang_author")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.LangDir)
                    .IsRequired()
                    .HasColumnName("lang_dir")
                    .HasMaxLength(30)
                    .IsUnicode(false);

                entity.Property(e => e.LangEnglishName)
                    .IsRequired()
                    .HasColumnName("lang_english_name")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.LangIso)
                    .IsRequired()
                    .HasColumnName("lang_iso")
                    .HasMaxLength(30)
                    .IsUnicode(false);

                entity.Property(e => e.LangLocalName)
                    .IsRequired()
                    .HasColumnName("lang_local_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<PhpbbLog>(entity =>
            {
                entity.HasKey(e => e.LogId);

                entity.ToTable("phpbb_log", "forum");

                entity.HasIndex(e => e.ForumId)
                    .HasDatabaseName("forum_id");

                entity.HasIndex(e => e.LogType)
                    .HasDatabaseName("log_type");

                entity.HasIndex(e => e.ReporteeId)
                    .HasDatabaseName("reportee_id");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("user_id");

                entity.Property(e => e.LogId)
                    .HasColumnName("log_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.LogData)
                    .IsRequired()
                    .HasColumnName("log_data")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.LogIp)
                    .IsRequired()
                    .HasColumnName("log_ip")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.LogOperation)
                    .IsRequired()
                    .HasColumnName("log_operation")
                    .IsUnicode(false);

                entity.Property(e => e.LogTime)
                    .HasColumnName("log_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.LogType)
                    .HasColumnName("log_type")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ReporteeId)
                    .HasColumnName("reportee_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbPollOptions>(entity =>
            {
                entity.ToTable("phpbb_poll_options", "forum");

                entity.HasIndex(e => e.PollOptionId)
                    .HasDatabaseName("poll_opt_id");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.Property(e => e.PollOptionId)
                    .HasColumnName("poll_option_id")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PollOptionText)
                    .IsRequired()
                    .HasColumnName("poll_option_text")
                    .IsUnicode(false);

                entity.Property(e => e.PollOptionTotal)
                    .HasColumnName("poll_option_total")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.HasNoKey();
            });

            modelBuilder.Entity<PhpbbPollVotes>(entity =>
            {
                entity.ToTable("phpbb_poll_votes", "forum");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.HasIndex(e => e.VoteUserId)
                    .HasDatabaseName("vote_user_id");

                entity.HasIndex(e => e.VoteUserIp)
                    .HasDatabaseName("vote_user_ip");

                entity.Property(e => e.PollOptionId)
                    .HasColumnName("poll_option_id")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.VoteUserId)
                    .HasColumnName("vote_user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.VoteUserIp)
                    .IsRequired()
                    .HasColumnName("vote_user_ip")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.HasNoKey();
            });

            modelBuilder.Entity<PhpbbPosts>(entity =>
            {
                entity.HasKey(e => e.PostId);

                entity.ToTable("phpbb_posts", "forum");

                entity.HasIndex(e => e.ForumId)
                    .HasDatabaseName("forum_id");

                entity.HasIndex(e => e.PostApproved)
                    .HasDatabaseName("post_approved");

                entity.HasIndex(e => e.PostSubject)
                    .HasDatabaseName("post_subject");

                entity.HasIndex(e => e.PostText)
                    .HasDatabaseName("post_text");

                entity.HasIndex(e => e.PostUsername)
                    .HasDatabaseName("post_username");

                entity.HasIndex(e => e.PosterId)
                    .HasDatabaseName("poster_id");

                entity.HasIndex(e => e.PosterIp)
                    .HasDatabaseName("poster_ip");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.HasIndex(e => new { e.PostSubject, e.PostText })
                    .HasDatabaseName("post_content");

                entity.HasIndex(e => new { e.TopicId, e.PostTime })
                    .HasDatabaseName("tid_post_time");

                entity.Property(e => e.PostId)
                    .HasColumnName("post_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.BbcodeBitfield)
                    .IsRequired()
                    .HasColumnName("bbcode_bitfield")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BbcodeUid)
                    .IsRequired()
                    .HasColumnName("bbcode_uid")
                    .HasMaxLength(8)
                    .IsUnicode(false);

                entity.Property(e => e.EnableBbcode)
                    .HasColumnName("enable_bbcode")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableMagicUrl)
                    .HasColumnName("enable_magic_url")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableSig)
                    .HasColumnName("enable_sig")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableSmilies)
                    .HasColumnName("enable_smilies")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.IconId)
                    .HasColumnName("icon_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostApproved)
                    .HasColumnName("post_approved")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.PostAttachment)
                    .HasColumnName("post_attachment")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostChecksum)
                    .IsRequired()
                    .HasColumnName("post_checksum")
                    .HasMaxLength(32)
                    .IsUnicode(false);

                entity.Property(e => e.PostEditCount)
                    .HasColumnName("post_edit_count")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostEditLocked)
                    .HasColumnName("post_edit_locked")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostEditReason)
                    .IsRequired()
                    .HasColumnName("post_edit_reason")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.PostEditTime)
                    .HasColumnName("post_edit_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostEditUser)
                    .HasColumnName("post_edit_user")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostPostcount)
                    .HasColumnName("post_postcount")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.PostReported)
                    .HasColumnName("post_reported")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostSubject)
                    .IsRequired()
                    .HasColumnName("post_subject")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.PostText)
                    .IsRequired()
                    .HasColumnName("post_text")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.PostTime)
                    .HasColumnName("post_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostUsername)
                    .IsRequired()
                    .HasColumnName("post_username")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.PosterId)
                    .HasColumnName("poster_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PosterIp)
                    .IsRequired()
                    .HasColumnName("poster_ip")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbPrivmsgs>(entity =>
            {
                entity.HasKey(e => e.MsgId);

                entity.ToTable("phpbb_privmsgs", "forum");

                entity.HasIndex(e => e.AuthorId)
                    .HasDatabaseName("author_id");

                entity.HasIndex(e => e.AuthorIp)
                    .HasDatabaseName("author_ip");

                entity.HasIndex(e => e.MessageTime)
                    .HasDatabaseName("message_time");

                entity.HasIndex(e => e.RootLevel)
                    .HasDatabaseName("root_level");

                entity.Property(e => e.MsgId)
                    .HasColumnName("msg_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.AuthorId)
                    .HasColumnName("author_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.AuthorIp)
                    .IsRequired()
                    .HasColumnName("author_ip")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.BbcodeBitfield)
                    .IsRequired()
                    .HasColumnName("bbcode_bitfield")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.BbcodeUid)
                    .IsRequired()
                    .HasColumnName("bbcode_uid")
                    .HasMaxLength(8)
                    .IsUnicode(false);

                entity.Property(e => e.BccAddress)
                    .IsRequired()
                    .HasColumnName("bcc_address")
                    .IsUnicode(false);

                entity.Property(e => e.EnableBbcode)
                    .HasColumnName("enable_bbcode")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableMagicUrl)
                    .HasColumnName("enable_magic_url")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableSig)
                    .HasColumnName("enable_sig")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.EnableSmilies)
                    .HasColumnName("enable_smilies")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.IconId)
                    .HasColumnName("icon_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MessageAttachment)
                    .HasColumnName("message_attachment")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MessageEditCount)
                    .HasColumnName("message_edit_count")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MessageEditReason)
                    .IsRequired()
                    .HasColumnName("message_edit_reason")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.MessageEditTime)
                    .HasColumnName("message_edit_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MessageEditUser)
                    .HasColumnName("message_edit_user")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MessageReported)
                    .HasColumnName("message_reported")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MessageSubject)
                    .IsRequired()
                    .HasColumnName("message_subject")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.MessageText)
                    .IsRequired()
                    .HasColumnName("message_text")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.MessageTime)
                    .HasColumnName("message_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.RootLevel)
                    .HasColumnName("root_level")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ToAddress)
                    .IsRequired()
                    .HasColumnName("to_address")
                    .IsUnicode(false);
            });

            modelBuilder.Entity<PhpbbPrivmsgsTo>(entity =>
            {
                entity.ToTable("phpbb_privmsgs_to", "forum");

                entity.HasIndex(e => e.AuthorId)
                    .HasDatabaseName("author_id");

                entity.HasIndex(e => e.MsgId)
                    .HasDatabaseName("msg_id");

                entity.HasIndex(e => new { e.UserId, e.FolderId })
                    .HasDatabaseName("usr_flder_id");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("bigint(20)");

                entity.Property(e => e.AuthorId)
                    .HasColumnName("author_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.FolderId)
                    .HasColumnName("folder_id")
                    .HasColumnType("int(11)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MsgId)
                    .HasColumnName("msg_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PmDeleted)
                    .HasColumnName("pm_deleted")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PmForwarded)
                    .HasColumnName("pm_forwarded")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PmMarked)
                    .HasColumnName("pm_marked")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PmNew)
                    .HasColumnName("pm_new")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.PmReplied)
                    .HasColumnName("pm_replied")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PmUnread)
                    .HasColumnName("pm_unread")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbRanks>(entity =>
            {
                entity.HasKey(e => e.RankId);

                entity.ToTable("phpbb_ranks", "forum");

                entity.Property(e => e.RankId)
                    .HasColumnName("rank_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.RankImage)
                    .IsRequired()
                    .HasColumnName("rank_image")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.RankMin)
                    .HasColumnName("rank_min")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.RankSpecial)
                    .HasColumnName("rank_special")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.RankTitle)
                    .IsRequired()
                    .HasColumnName("rank_title")
                    .HasMaxLength(255)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<PhpbbReports>(entity =>
            {
                entity.HasKey(e => e.ReportId);

                entity.ToTable("phpbb_reports", "forum");

                entity.HasIndex(e => e.PmId)
                    .HasDatabaseName("pm_id");

                entity.HasIndex(e => e.PostId)
                    .HasDatabaseName("post_id");

                entity.Property(e => e.ReportId)
                    .HasColumnName("report_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.PmId)
                    .HasColumnName("pm_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PostId)
                    .HasColumnName("post_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ReasonId)
                    .HasColumnName("reason_id")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ReportClosed)
                    .HasColumnName("report_closed")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ReportText)
                    .IsRequired()
                    .HasColumnName("report_text")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.ReportTime)
                    .HasColumnName("report_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserNotify)
                    .HasColumnName("user_notify")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbReportsReasons>(entity =>
            {
                entity.HasKey(e => e.ReasonId);

                entity.ToTable("phpbb_reports_reasons", "forum");

                entity.Property(e => e.ReasonId)
                    .HasColumnName("reason_id")
                    .HasColumnType("smallint(4) unsigned");

                entity.Property(e => e.ReasonDescription)
                    .IsRequired()
                    .HasColumnName("reason_description")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.ReasonOrder)
                    .HasColumnName("reason_order")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ReasonTitle)
                    .IsRequired()
                    .HasColumnName("reason_title")
                    .HasMaxLength(255)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<PhpbbSmilies>(entity =>
            {
                entity.HasKey(e => e.SmileyId);

                entity.ToTable("phpbb_smilies", "forum");

                entity.HasIndex(e => e.DisplayOnPosting)
                    .HasDatabaseName("display_on_post");

                entity.Property(e => e.SmileyId)
                    .HasColumnName("smiley_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasColumnName("code")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.DisplayOnPosting)
                    .HasColumnName("display_on_posting")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.Emotion)
                    .IsRequired()
                    .HasColumnName("emotion")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.SmileyHeight)
                    .HasColumnName("smiley_height")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.SmileyOrder)
                    .HasColumnName("smiley_order")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.SmileyUrl)
                    .IsRequired()
                    .HasColumnName("smiley_url")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.SmileyWidth)
                    .HasColumnName("smiley_width")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbStyles>(entity =>
            {
                entity.HasKey(e => e.StyleId);

                entity.ToTable("phpbb_styles", "forum");

                entity.HasIndex(e => e.ImagesetId)
                    .HasDatabaseName("imageset_id");

                entity.HasIndex(e => e.StyleName)
                    .HasDatabaseName("style_name")
                    .IsUnique();

                entity.HasIndex(e => e.TemplateId)
                    .HasDatabaseName("template_id");

                entity.HasIndex(e => e.ThemeId)
                    .HasDatabaseName("theme_id");

                entity.Property(e => e.StyleId)
                    .HasColumnName("style_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.ImagesetId)
                    .HasColumnName("imageset_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.StyleActive)
                    .HasColumnName("style_active")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.StyleCopyright)
                    .IsRequired()
                    .HasColumnName("style_copyright")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.StyleName)
                    .IsRequired()
                    .HasColumnName("style_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.TemplateId)
                    .HasColumnName("template_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ThemeId)
                    .HasColumnName("theme_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbTopics>(entity =>
            {
                entity.HasKey(e => e.TopicId);

                entity.ToTable("phpbb_topics", "forum");

                entity.HasIndex(e => e.ForumId)
                    .HasDatabaseName("forum_id");

                entity.HasIndex(e => e.TopicApproved)
                    .HasDatabaseName("topic_approved");

                entity.HasIndex(e => e.TopicLastPostTime)
                    .HasDatabaseName("last_post_time");

                entity.HasIndex(e => new { e.ForumId, e.TopicType })
                    .HasDatabaseName("forum_id_type");

                entity.HasIndex(e => new { e.ForumId, e.TopicApproved, e.TopicLastPostId })
                    .HasDatabaseName("forum_appr_last");

                entity.HasIndex(e => new { e.ForumId, e.TopicLastPostTime, e.TopicMovedId })
                    .HasDatabaseName("fid_time_moved");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.IconId)
                    .HasColumnName("icon_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PollLastVote)
                    .HasColumnName("poll_last_vote")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PollLength)
                    .HasColumnName("poll_length")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PollMaxOptions)
                    .HasColumnName("poll_max_options")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.PollStart)
                    .HasColumnName("poll_start")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.PollTitle)
                    .IsRequired()
                    .HasColumnName("poll_title")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.PollVoteChange)
                    .HasColumnName("poll_vote_change")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicApproved)
                    .HasColumnName("topic_approved")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.TopicAttachment)
                    .HasColumnName("topic_attachment")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicBumped)
                    .HasColumnName("topic_bumped")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicBumper)
                    .HasColumnName("topic_bumper")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicFirstPostId)
                    .HasColumnName("topic_first_post_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicFirstPosterColour)
                    .IsRequired()
                    .HasColumnName("topic_first_poster_colour")
                    .HasMaxLength(6)
                    .IsUnicode(false);

                entity.Property(e => e.TopicFirstPosterName)
                    .IsRequired()
                    .HasColumnName("topic_first_poster_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.TopicLastPostId)
                    .HasColumnName("topic_last_post_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicLastPostSubject)
                    .IsRequired()
                    .HasColumnName("topic_last_post_subject")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.TopicLastPostTime)
                    .HasColumnName("topic_last_post_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicLastPosterColour)
                    .IsRequired()
                    .HasColumnName("topic_last_poster_colour")
                    .HasMaxLength(6)
                    .IsUnicode(false);

                entity.Property(e => e.TopicLastPosterId)
                    .HasColumnName("topic_last_poster_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicLastPosterName)
                    .IsRequired()
                    .HasColumnName("topic_last_poster_name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.TopicLastViewTime)
                    .HasColumnName("topic_last_view_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicMovedId)
                    .HasColumnName("topic_moved_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicPoster)
                    .HasColumnName("topic_poster")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicReplies)
                    .HasColumnName("topic_replies")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicRepliesReal)
                    .HasColumnName("topic_replies_real")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicReported)
                    .HasColumnName("topic_reported")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicStatus)
                    .HasColumnName("topic_status")
                    .HasColumnType("tinyint(3)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicTime)
                    .HasColumnName("topic_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicTimeLimit)
                    .HasColumnName("topic_time_limit")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicTitle)
                    .IsRequired()
                    .HasColumnName("topic_title")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.TopicType)
                    .HasColumnName("topic_type")
                    .HasColumnType("tinyint(3)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicViews)
                    .HasColumnName("topic_views")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbTopicsTrack>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.TopicId });

                entity.ToTable("phpbb_topics_track", "forum");

                entity.HasIndex(e => e.ForumId)
                    .HasDatabaseName("forum_id");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.MarkTime)
                    .HasColumnName("mark_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbTopicsWatch>(entity =>
            {
                entity.HasKey(e => new { e.TopicId, e.UserId });

                entity.ToTable("phpbb_topics_watch", "forum");

                entity.HasIndex(e => e.NotifyStatus)
                    .HasDatabaseName("notify_stat");

                entity.HasIndex(e => e.TopicId)
                    .HasDatabaseName("topic_id");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("user_id");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.NotifyStatus)
                    .HasColumnName("notify_status")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbUserGroup>(entity =>
            {
                entity.HasKey(e => new { e.GroupId, e.UserId });

                entity.ToTable("phpbb_user_group", "forum");

                entity.HasIndex(e => e.GroupId)
                    .HasDatabaseName("group_id");

                entity.HasIndex(e => e.GroupLeader)
                    .HasDatabaseName("group_leader");

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("user_id");

                entity.Property(e => e.GroupId)
                    .HasColumnName("group_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.GroupLeader)
                    .HasColumnName("group_leader")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserPending)
                    .HasColumnName("user_pending")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");
            });

            modelBuilder.Entity<PhpbbUserTopicPostNumber>(entity =>
            {
                entity.ToTable("phpbb_user_topic_post_number", "forum");

                entity.HasKey(e => new { e.UserId, e.TopicId });

                entity.HasIndex(e => new { e.UserId, e.TopicId })
                    .HasDatabaseName("user_id");

                entity.Property(e => e.PostNo)
                    .HasColumnName("post_no")
                    .HasColumnType("int(11)");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("int(11)");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("int(11)");
            });

            modelBuilder.Entity<PhpbbUsers>(entity =>
            {
                entity.HasKey(e => e.UserId);

                entity.ToTable("phpbb_users", "forum");

                entity.HasIndex(e => e.UserBirthday)
                    .HasDatabaseName("user_birthday");

                entity.HasIndex(e => e.UserEmailHash)
                    .HasDatabaseName("user_email_hash");

                entity.HasIndex(e => e.UserType)
                    .HasDatabaseName("user_type");

                entity.HasIndex(e => e.UsernameClean)
                    .HasDatabaseName("username_clean")
                    .IsUnique();

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.AcceptedNewTerms)
                    .HasColumnName("accepted_new_terms")
                    .HasColumnType("tinyint(4)");

                entity.Property(e => e.GroupId)
                    .HasColumnName("group_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("3");

                entity.Property(e => e.UserActkey)
                    .IsRequired()
                    .HasColumnName("user_actkey")
                    .HasMaxLength(32)
                    .IsUnicode(false);

                entity.Property(e => e.UserAim)
                    .IsRequired()
                    .HasColumnName("user_aim")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UserAllowMassemail)
                    .HasColumnName("user_allow_massemail")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserAllowPm)
                    .HasColumnName("user_allow_pm")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserAllowViewemail)
                    .HasColumnName("user_allow_viewemail")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserAllowViewonline)
                    .HasColumnName("user_allow_viewonline")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserAvatar)
                    .IsRequired()
                    .HasColumnName("user_avatar")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UserAvatarHeight)
                    .HasColumnName("user_avatar_height")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserAvatarType)
                    .HasColumnName("user_avatar_type")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserAvatarWidth)
                    .HasColumnName("user_avatar_width")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserBirthday)
                    .IsRequired()
                    .HasColumnName("user_birthday")
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(e => e.UserColour)
                    .IsRequired()
                    .HasColumnName("user_colour")
                    .HasMaxLength(6)
                    .IsUnicode(false);

                entity.Property(e => e.UserDateformat)
                    .IsRequired()
                    .HasColumnName("user_dateformat")
                    .HasMaxLength(30)
                    .IsUnicode(false)
                    .HasDefaultValueSql("d M Y H:i");

                entity.Property(e => e.UserDst)
                    .HasColumnName("user_dst")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserEditTime)
                    .HasColumnName("user_edit_time")
                    .HasColumnType("int(4) unsigned")
                    .HasDefaultValueSql("60");

                entity.Property(e => e.UserEmail)
                    .IsRequired()
                    .HasColumnName("user_email")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.UserEmailHash)
                    .HasColumnName("user_email_hash")
                    .HasColumnType("bigint(20)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserEmailtime)
                    .HasColumnName("user_emailtime")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserFormSalt)
                    .IsRequired()
                    .HasColumnName("user_form_salt")
                    .HasMaxLength(32)
                    .IsUnicode(false);

                entity.Property(e => e.UserFrom)
                    .IsRequired()
                    .HasColumnName("user_from")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.UserFullFolder)
                    .HasColumnName("user_full_folder")
                    .HasColumnType("int(11)")
                    .HasDefaultValueSql("-3");

                entity.Property(e => e.UserIcq)
                    .IsRequired()
                    .HasColumnName("user_icq")
                    .HasMaxLength(15)
                    .IsUnicode(false);

                entity.Property(e => e.UserInactiveReason)
                    .HasColumnName("user_inactive_reason")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserInactiveTime)
                    .HasColumnName("user_inactive_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserInterests)
                    .IsRequired()
                    .HasColumnName("user_interests")
                    .IsUnicode(false);

                entity.Property(e => e.UserIp)
                    .IsRequired()
                    .HasColumnName("user_ip")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.UserJabber)
                    .IsRequired()
                    .HasColumnName("user_jabber")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UserLang)
                    .IsRequired()
                    .HasColumnName("user_lang")
                    .HasMaxLength(30)
                    .IsUnicode(false);

                entity.Property(e => e.UserLastConfirmKey)
                    .IsRequired()
                    .HasColumnName("user_last_confirm_key")
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(e => e.UserLastPrivmsg)
                    .HasColumnName("user_last_privmsg")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserLastSearch)
                    .HasColumnName("user_last_search")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserLastWarning)
                    .HasColumnName("user_last_warning")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserLastmark)
                    .HasColumnName("user_lastmark")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserLastpage)
                    .IsRequired()
                    .HasColumnName("user_lastpage")
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.UserLastpostTime)
                    .HasColumnName("user_lastpost_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserLastvisit)
                    .HasColumnName("user_lastvisit")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserLoginAttempts)
                    .HasColumnName("user_login_attempts")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserMessageRules)
                    .HasColumnName("user_message_rules")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserMsnm)
                    .IsRequired()
                    .HasColumnName("user_msnm")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UserNew)
                    .HasColumnName("user_new")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserNewPrivmsg)
                    .HasColumnName("user_new_privmsg")
                    .HasColumnType("int(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserNewpasswd)
                    .IsRequired()
                    .HasColumnName("user_newpasswd")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.UserNotify)
                    .HasColumnName("user_notify")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserNotifyPm)
                    .HasColumnName("user_notify_pm")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.UserNotifyType)
                    .HasColumnName("user_notify_type")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserOcc)
                    .IsRequired()
                    .HasColumnName("user_occ")
                    .IsUnicode(false);

                entity.Property(e => e.UserOptions)
                    .HasColumnName("user_options")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("230271");

                entity.Property(e => e.UserPassConvert)
                    .HasColumnName("user_pass_convert")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserPasschg)
                    .HasColumnName("user_passchg")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserPassword)
                    .IsRequired()
                    .HasColumnName("user_password")
                    .HasMaxLength(40)
                    .IsUnicode(false);

                entity.Property(e => e.UserPermFrom)
                    .HasColumnName("user_perm_from")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserPermissions)
                    .IsRequired()
                    .HasColumnName("user_permissions")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.UserPostShowDays)
                    .HasColumnName("user_post_show_days")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserPostSortbyDir)
                    .IsRequired()
                    .HasColumnName("user_post_sortby_dir")
                    .HasColumnType("char(1)")
                    .HasDefaultValueSql("a");

                entity.Property(e => e.UserPostSortbyType)
                    .IsRequired()
                    .HasColumnName("user_post_sortby_type")
                    .HasColumnType("char(1)")
                    .HasDefaultValueSql("t");

                entity.Property(e => e.UserPosts)
                    .HasColumnName("user_posts")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserRank)
                    .HasColumnName("user_rank")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserRegdate)
                    .HasColumnName("user_regdate")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserReminded)
                    .HasColumnName("user_reminded")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserRemindedTime)
                    .HasColumnName("user_reminded_time")
                    .HasColumnType("int(11) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserSig)
                    .IsRequired()
                    .HasColumnName("user_sig")
                    .HasColumnType("mediumtext");

                entity.Property(e => e.UserSigBbcodeBitfield)
                    .IsRequired()
                    .HasColumnName("user_sig_bbcode_bitfield")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UserSigBbcodeUid)
                    .IsRequired()
                    .HasColumnName("user_sig_bbcode_uid")
                    .HasMaxLength(8)
                    .IsUnicode(false);

                entity.Property(e => e.UserStyle)
                    .HasColumnName("user_style")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserTimezone)
                    .HasColumnName("user_timezone")
                    .HasColumnType("decimal(5,2)")
                    .HasDefaultValueSql("0.00");

                entity.Property(e => e.UserTopicShowDays)
                    .HasColumnName("user_topic_show_days")
                    .HasColumnType("smallint(4) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserTopicSortbyDir)
                    .IsRequired()
                    .HasColumnName("user_topic_sortby_dir")
                    .HasColumnType("char(1)")
                    .HasDefaultValueSql("d");

                entity.Property(e => e.UserTopicSortbyType)
                    .IsRequired()
                    .HasColumnName("user_topic_sortby_type")
                    .HasColumnType("char(1)")
                    .HasDefaultValueSql("t");

                entity.Property(e => e.UserType)
                    .HasColumnName("user_type")
                    .HasColumnType("tinyint(2)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserUnreadPrivmsg)
                    .HasColumnName("user_unread_privmsg")
                    .HasColumnType("int(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserWarnings)
                    .HasColumnName("user_warnings")
                    .HasColumnType("tinyint(4)")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.UserWebsite)
                    .IsRequired()
                    .HasColumnName("user_website")
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.UserYim)
                    .IsRequired()
                    .HasColumnName("user_yim")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasColumnName("username")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.UsernameClean)
                    .IsRequired()
                    .HasColumnName("username_clean")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.JumpToUnread)
                    .HasColumnName("jump_to_unread")
                    .HasColumnType("tinyint(1)")
                    .HasDefaultValueSql("1");
            });

            modelBuilder.Entity<PhpbbWords>(entity =>
            {
                entity.HasKey(e => e.WordId);

                entity.ToTable("phpbb_words", "forum");

                entity.Property(e => e.WordId)
                    .HasColumnName("word_id")
                    .HasColumnType("mediumint(8) unsigned");

                entity.Property(e => e.Replacement)
                    .IsRequired()
                    .HasColumnName("replacement")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Word)
                    .IsRequired()
                    .HasColumnName("word")
                    .HasMaxLength(255)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<PhpbbZebra>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ZebraId });

                entity.ToTable("phpbb_zebra", "forum");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.ZebraId)
                    .HasColumnName("zebra_id")
                    .HasColumnType("mediumint(8) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.Foe)
                    .HasColumnName("foe")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.Friend)
                    .HasColumnName("friend")
                    .HasColumnType("tinyint(1) unsigned")
                    .HasDefaultValueSql("0");
            });

            modelBuilder.Entity<PhpbbRecycleBin>(entity =>
            {
                entity.HasKey(e => new { e.Type, e.Id });

                entity.ToTable("phpbb_recycle_bin", "forum");

                entity.Property(e => e.Type)
                    .HasColumnName("type")
                    .HasColumnType("int");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("int");

                entity.Property(e => e.Content)
                    .HasColumnName("content")
                    .HasColumnType("longblob");

                entity.Property(e => e.DeleteTime)
                    .HasColumnName("delete_time")
                    .HasColumnType("int(11) unsigned");

                entity.Property(e => e.DeleteUser)
                    .HasColumnName("delete_user")
                    .HasColumnType("int(8) unsigned");
            });

            modelBuilder.Entity<PhpbbShortcuts>(entity =>
            {
                entity.HasKey(e => new { e.TopicId, e.ForumId });

                entity.ToTable("phpbb_shortcuts", "forum");

                entity.Property(e => e.TopicId)
                    .HasColumnName("topic_id")
                    .HasColumnType("int(8) unsigned");

                entity.Property(e => e.ForumId)
                    .HasColumnName("forum_id")
                    .HasColumnType("int(8) unsigned");
            });
        }

        public async Task<ISqlExecuter> GetSqlExecuterAsync()
        {
            var conn = Database.GetDbConnection();
            if (conn.State == ConnectionState.Closed)
            {
                await conn.OpenAsync();
            }
            return new SqlExecuter(conn);
        }

        public ISqlExecuter GetSqlExecuter()
        {
            var conn = Database.GetDbConnection();
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            return new SqlExecuter(conn);
        }
    }
}
