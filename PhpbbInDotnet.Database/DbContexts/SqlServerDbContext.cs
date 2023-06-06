using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using System;
using System.Linq.Expressions;

namespace PhpbbInDotnet.Database.DbContexts
{
    class SqlServerDbContext : ForumDbContext
    {
        public SqlServerDbContext(DbContextOptions<SqlServerDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseCollation("Latin1_General_CI_AI");

            modelBuilder.Entity<PhpbbAclGroups>(entity =>
            {
                entity.HasKey(e => new { e.GroupId, e.ForumId, e.AuthOptionId, e.AuthRoleId })
                    .HasName("PK_phpbb_acl_groups_group_id");

                entity.ToTable("phpbb_acl_groups");

                entity.HasIndex(e => e.AuthOptionId, "auth_opt_id");

                entity.HasIndex(e => e.AuthRoleId, "auth_role_id");

                entity.HasIndex(e => e.GroupId, "group_id");

                entity.Property(e => e.GroupId).HasColumnName("group_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.AuthOptionId).HasColumnName("auth_option_id");

                entity.Property(e => e.AuthRoleId).HasColumnName("auth_role_id");

                entity.Property(e => e.AuthSetting).HasColumnName("auth_setting");
            });

            modelBuilder.Entity<PhpbbAclOptions>(entity =>
            {
                entity.HasKey(e => e.AuthOptionId)
                    .HasName("PK_phpbb_acl_options_auth_option_id");

                entity.ToTable("phpbb_acl_options");

                entity.HasIndex(e => e.AuthOption, "phpbb_acl_options$auth_option")
                    .IsUnique();

                entity.Property(e => e.AuthOptionId).HasColumnName("auth_option_id");

                entity.Property(e => e.AuthOption)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnName("auth_option")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.FounderOnly).HasColumnName("founder_only");

                entity.Property(e => e.IsGlobal).HasColumnName("is_global");

                entity.Property(e => e.IsLocal).HasColumnName("is_local");
            });

            modelBuilder.Entity<PhpbbAclRoles>(entity =>
            {
                entity.HasKey(e => e.RoleId)
                    .HasName("PK_phpbb_acl_roles_role_id");

                entity.ToTable("phpbb_acl_roles");

                entity.HasIndex(e => e.RoleOrder, "role_order");

                entity.HasIndex(e => e.RoleType, "role_type");

                entity.Property(e => e.RoleId).HasColumnName("role_id");

                entity.Property(e => e.RoleDescription)
                    .IsRequired()
                    .HasColumnName("role_description");

                entity.Property(e => e.RoleName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("role_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.RoleOrder).HasColumnName("role_order");

                entity.Property(e => e.RoleType)
                    .IsRequired()
                    .HasMaxLength(10)
                    .HasColumnName("role_type")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbAclRolesData>(entity =>
            {
                entity.HasKey(e => new { e.RoleId, e.AuthOptionId })
                    .HasName("PK_phpbb_acl_roles_data_role_id");

                entity.ToTable("phpbb_acl_roles_data");

                entity.HasIndex(e => e.AuthOptionId, "ath_op_id");

                entity.Property(e => e.RoleId).HasColumnName("role_id");

                entity.Property(e => e.AuthOptionId).HasColumnName("auth_option_id");

                entity.Property(e => e.AuthSetting).HasColumnName("auth_setting");
            });

            modelBuilder.Entity<PhpbbAclUsers>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ForumId, e.AuthOptionId, e.AuthRoleId })
                    .HasName("PK_phpbb_acl_users_user_id");

                entity.ToTable("phpbb_acl_users");

                entity.HasIndex(e => e.AuthOptionId, "auth_option_id");

                entity.HasIndex(e => e.AuthRoleId, "auth_role_id");

                entity.HasIndex(e => e.UserId, "user_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.AuthOptionId).HasColumnName("auth_option_id");

                entity.Property(e => e.AuthRoleId).HasColumnName("auth_role_id");

                entity.Property(e => e.AuthSetting).HasColumnName("auth_setting");
            });

            modelBuilder.Entity<PhpbbAttachments>(entity =>
            {
                entity.HasKey(e => e.AttachId)
                    .HasName("PK_phpbb_attachments_attach_id");

                entity.ToTable("phpbb_attachments");

                entity.HasIndex(e => e.Filetime, "filetime");

                entity.HasIndex(e => e.IsOrphan, "is_orphan");

                entity.HasIndex(e => e.PostMsgId, "post_msg_id");

                entity.HasIndex(e => e.PosterId, "poster_id");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.Property(e => e.AttachId).HasColumnName("attach_id");

                entity.Property(e => e.AttachComment)
                    .IsRequired()
                    .HasColumnName("attach_comment");

                entity.Property(e => e.DownloadCount).HasColumnName("download_count");

                entity.Property(e => e.Extension)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("extension")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.Filesize).HasColumnName("filesize");

                entity.Property(e => e.Filetime).HasColumnName("filetime");

                entity.Property(e => e.InMessage).HasColumnName("in_message");

                entity.Property(e => e.IsOrphan)
                    .HasColumnName("is_orphan")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.Mimetype)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("mimetype")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PhysicalFilename)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("physical_filename")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PostMsgId).HasColumnName("post_msg_id");

                entity.Property(e => e.PosterId).HasColumnName("poster_id");

                entity.Property(e => e.RealFilename)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("real_filename")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.Thumbnail).HasColumnName("thumbnail");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");
            });

            modelBuilder.Entity<PhpbbBanlist>(entity =>
            {
                entity.HasKey(e => e.BanId)
                    .HasName("PK_phpbb_banlist_ban_id");

                entity.ToTable("phpbb_banlist");

                entity.HasIndex(e => new { e.BanEmail, e.BanExclude }, "ban_email");

                entity.HasIndex(e => e.BanEnd, "ban_end");

                entity.HasIndex(e => new { e.BanIp, e.BanExclude }, "ban_ip");

                entity.HasIndex(e => new { e.BanUserid, e.BanExclude }, "ban_user");

                entity.Property(e => e.BanId).HasColumnName("ban_id");

                entity.Property(e => e.BanEmail)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("ban_email")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BanEnd).HasColumnName("ban_end");

                entity.Property(e => e.BanExclude).HasColumnName("ban_exclude");

                entity.Property(e => e.BanGiveReason)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("ban_give_reason")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BanIp)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("ban_ip")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BanReason)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("ban_reason")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BanStart).HasColumnName("ban_start");

                entity.Property(e => e.BanUserid).HasColumnName("ban_userid");
            });

            modelBuilder.Entity<PhpbbBbcodes>(entity =>
            {
                entity.HasKey(e => e.BbcodeId)
                    .HasName("PK_phpbb_bbcodes_bbcode_id");

                entity.ToTable("phpbb_bbcodes");

                entity.HasIndex(e => e.DisplayOnPosting, "display_on_post");

                entity.Property(e => e.BbcodeId).HasColumnName("bbcode_id");

                entity.Property(e => e.BbcodeHelpline)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("bbcode_helpline")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BbcodeMatch)
                    .IsRequired()
                    .HasColumnName("bbcode_match");

                entity.Property(e => e.BbcodeTag)
                    .IsRequired()
                    .HasMaxLength(16)
                    .HasColumnName("bbcode_tag")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BbcodeTpl)
                    .IsRequired()
                    .HasColumnName("bbcode_tpl");

                entity.Property(e => e.DisplayOnPosting).HasColumnName("display_on_posting");

                entity.Property(e => e.FirstPassMatch)
                    .IsRequired()
                    .HasColumnName("first_pass_match");

                entity.Property(e => e.FirstPassReplace)
                    .IsRequired()
                    .HasColumnName("first_pass_replace");

                entity.Property(e => e.SecondPassMatch)
                    .IsRequired()
                    .HasColumnName("second_pass_match");

                entity.Property(e => e.SecondPassReplace)
                    .IsRequired()
                    .HasColumnName("second_pass_replace");
            });

            modelBuilder.Entity<PhpbbBots>(entity =>
            {
                entity.HasKey(e => e.BotId)
                    .HasName("PK_phpbb_bots_bot_id");

                entity.ToTable("phpbb_bots");

                entity.HasIndex(e => e.BotActive, "bot_active");

                entity.Property(e => e.BotId).HasColumnName("bot_id");

                entity.Property(e => e.BotActive)
                    .HasColumnName("bot_active")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.BotAgent)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("bot_agent")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BotIp)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("bot_ip")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BotName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("bot_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<PhpbbDrafts>(entity =>
            {
                entity.HasKey(e => e.DraftId)
                    .HasName("PK_phpbb_drafts_draft_id");

                entity.ToTable("phpbb_drafts");

                entity.HasIndex(e => e.SaveTime, "save_time");

                entity.Property(e => e.DraftId).HasColumnName("draft_id");

                entity.Property(e => e.DraftMessage)
                    .IsRequired()
                    .HasColumnName("draft_message");

                entity.Property(e => e.DraftSubject)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("draft_subject")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.SaveTime).HasColumnName("save_time");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<PhpbbForums>(entity =>
            {
                entity.HasKey(e => e.ForumId)
                    .HasName("PK_phpbb_forums_forum_id");

                entity.ToTable("phpbb_forums");

                entity.HasIndex(e => e.ForumLastPostId, "forum_lastpost_id");

                entity.HasIndex(e => new { e.LeftId, e.RightId }, "left_right_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.DisplayOnIndex)
                    .HasColumnName("display_on_index")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.DisplaySubforumList)
                    .HasColumnName("display_subforum_list")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableIcons)
                    .HasColumnName("enable_icons")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableIndexing)
                    .HasColumnName("enable_indexing")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnablePrune).HasColumnName("enable_prune");

                entity.Property(e => e.ForumDesc)
                    .IsRequired()
                    .HasColumnName("forum_desc");

                entity.Property(e => e.ForumDescBitfield)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_desc_bitfield")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumDescOptions)
                    .HasColumnName("forum_desc_options")
                    .HasDefaultValueSql("((7))");

                entity.Property(e => e.ForumDescUid)
                    .IsRequired()
                    .HasMaxLength(8)
                    .HasColumnName("forum_desc_uid")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumEditTime).HasColumnName("forum_edit_time");

                entity.Property(e => e.ForumFlags)
                    .HasColumnName("forum_flags")
                    .HasDefaultValueSql("((32))");

                entity.Property(e => e.ForumImage)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_image")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumLastPostId).HasColumnName("forum_last_post_id");

                entity.Property(e => e.ForumLastPostSubject)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_last_post_subject")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumLastPostTime).HasColumnName("forum_last_post_time");

                entity.Property(e => e.ForumLastPosterColour)
                    .IsRequired()
                    .HasMaxLength(6)
                    .HasColumnName("forum_last_poster_colour")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumLastPosterId).HasColumnName("forum_last_poster_id");

                entity.Property(e => e.ForumLastPosterName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_last_poster_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumLink)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_link")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumOptions).HasColumnName("forum_options");

                entity.Property(e => e.ForumParents)
                    .IsRequired()
                    .HasColumnName("forum_parents");

                entity.Property(e => e.ForumPassword)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("forum_password")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumPosts).HasColumnName("forum_posts");

                entity.Property(e => e.ForumRules)
                    .IsRequired()
                    .HasColumnName("forum_rules");

                entity.Property(e => e.ForumRulesBitfield)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_rules_bitfield")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumRulesLink)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("forum_rules_link")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumRulesOptions)
                    .HasColumnName("forum_rules_options")
                    .HasDefaultValueSql("((7))");

                entity.Property(e => e.ForumRulesUid)
                    .IsRequired()
                    .HasMaxLength(8)
                    .HasColumnName("forum_rules_uid")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ForumStatus).HasColumnName("forum_status");

                entity.Property(e => e.ForumStyle).HasColumnName("forum_style");

                entity.Property(e => e.ForumTopics).HasColumnName("forum_topics");

                entity.Property(e => e.ForumTopicsPerPage).HasColumnName("forum_topics_per_page");

                entity.Property(e => e.ForumTopicsReal).HasColumnName("forum_topics_real");

                entity.Property(e => e.ForumType).HasColumnName("forum_type");

                entity.Property(e => e.LeftId).HasColumnName("left_id");

                entity.Property(e => e.ParentId).HasColumnName("parent_id");

                entity.Property(e => e.PruneDays).HasColumnName("prune_days");

                entity.Property(e => e.PruneFreq).HasColumnName("prune_freq");

                entity.Property(e => e.PruneNext).HasColumnName("prune_next");

                entity.Property(e => e.PruneViewed).HasColumnName("prune_viewed");

                entity.Property(e => e.RightId).HasColumnName("right_id");
            });

            modelBuilder.Entity<PhpbbForumsTrack>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ForumId })
                    .HasName("PK_phpbb_forums_track_user_id");

                entity.ToTable("phpbb_forums_track");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.MarkTime).HasColumnName("mark_time");
            });

            modelBuilder.Entity<PhpbbForumsWatch>(entity =>
            {
                entity.HasKey(e => new { e.ForumId, e.UserId })
                    .HasName("PK_phpbb_forums_watch_forum_id");

                entity.ToTable("phpbb_forums_watch");

                entity.HasIndex(e => e.ForumId, "forum_id");

                entity.HasIndex(e => e.NotifyStatus, "notify_stat");

                entity.HasIndex(e => e.UserId, "user_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.NotifyStatus).HasColumnName("notify_status");
            });

            modelBuilder.Entity<PhpbbGroups>(entity =>
            {
                entity.HasKey(e => e.GroupId)
                    .HasName("PK_phpbb_groups_group_id");

                entity.ToTable("phpbb_groups");

                entity.HasIndex(e => new { e.GroupLegend, e.GroupName }, "group_legend_name");

                entity.Property(e => e.GroupId).HasColumnName("group_id");

                entity.Property(e => e.GroupAvatar)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("group_avatar")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.GroupAvatarHeight)
					.HasConversion(new IntToShortValueConverter())
					.HasColumnName("group_avatar_height");

                entity.Property(e => e.GroupAvatarType)
				    .HasConversion(new ByteToShortValueConverter())
				    .HasColumnName("group_avatar_type");

                entity.Property(e => e.GroupAvatarWidth)
					.HasConversion(new IntToShortValueConverter())
				    .HasColumnName("group_avatar_width");

                entity.Property(e => e.GroupColour)
                    .IsRequired()
                    .HasMaxLength(6)
                    .HasColumnName("group_colour")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.GroupDesc)
                    .IsRequired()
                    .HasColumnName("group_desc");

                entity.Property(e => e.GroupDescBitfield)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("group_desc_bitfield")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.GroupDescOptions)
                    .HasColumnName("group_desc_options")
                    .HasDefaultValueSql("((7))");

                entity.Property(e => e.GroupDescUid)
                    .IsRequired()
                    .HasMaxLength(8)
                    .HasColumnName("group_desc_uid")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.GroupDisplay).HasColumnName("group_display");

                entity.Property(e => e.GroupEditTime)
                    .HasColumnName("group_edit_time")
                    .HasDefaultValueSql("((60))");

                entity.Property(e => e.GroupFounderManage).HasColumnName("group_founder_manage");

                entity.Property(e => e.GroupLegend)
                    .HasColumnName("group_legend")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.GroupMaxRecipients).HasColumnName("group_max_recipients");

                entity.Property(e => e.GroupMessageLimit).HasColumnName("group_message_limit");

                entity.Property(e => e.GroupName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("group_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.GroupRank).HasColumnName("group_rank");

                entity.Property(e => e.GroupReceivePm).HasColumnName("group_receive_pm");

                entity.Property(e => e.GroupSigChars).HasColumnName("group_sig_chars");

                entity.Property(e => e.GroupSkipAuth).HasColumnName("group_skip_auth");

                entity.Property(e => e.GroupType)
                    .HasColumnName("group_type")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.GroupUserUploadSize).HasColumnName("group_user_upload_size");
            });

            modelBuilder.Entity<PhpbbLang>(entity =>
            {
                entity.HasKey(e => e.LangId)
                    .HasName("PK_phpbb_lang_lang_id");

                entity.ToTable("phpbb_lang");

                entity.HasIndex(e => e.LangIso, "lang_iso");

                entity.Property(e => e.LangId).HasColumnName("lang_id");

                entity.Property(e => e.LangAuthor)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("lang_author")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.LangDir)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasColumnName("lang_dir")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.LangEnglishName)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("lang_english_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.LangIso)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasColumnName("lang_iso")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.LangLocalName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("lang_local_name")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbLog>(entity =>
            {
                entity.HasKey(e => e.LogId)
                    .HasName("PK_phpbb_log_log_id");

                entity.ToTable("phpbb_log");

                entity.HasIndex(e => e.ForumId, "forum_id");

                entity.HasIndex(e => e.LogType, "log_type");

                entity.HasIndex(e => e.ReporteeId, "reportee_id");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.HasIndex(e => e.UserId, "user_id");

                entity.Property(e => e.LogId).HasColumnName("log_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.LogData)
                    .IsRequired()
                    .HasColumnName("log_data");

                entity.Property(e => e.LogIp)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("log_ip")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.LogOperation)
                    .IsRequired()
                    .HasColumnName("log_operation");

                entity.Property(e => e.LogTime).HasColumnName("log_time");

                entity.Property(e => e.LogType).HasColumnName("log_type");

                entity.Property(e => e.ReporteeId).HasColumnName("reportee_id");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<PhpbbPollOptions>(entity =>
            {
                entity.HasNoKey();

                entity.ToTable("phpbb_poll_options");

                entity.HasIndex(e => e.PollOptionId, "poll_opt_id");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.Property(e => e.PollOptionId).HasColumnName("poll_option_id");

                entity.Property(e => e.PollOptionText)
                    .IsRequired()
                    .HasColumnName("poll_option_text");

                entity.Property(e => e.PollOptionTotal).HasColumnName("poll_option_total");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");
            });

            modelBuilder.Entity<PhpbbPollVotes>(entity =>
            {
                entity.HasNoKey();

                entity.ToTable("phpbb_poll_votes");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.HasIndex(e => e.VoteUserId, "vote_user_id");

                entity.HasIndex(e => e.VoteUserIp, "vote_user_ip");

                entity.Property(e => e.PollOptionId).HasColumnName("poll_option_id");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.VoteUserId).HasColumnName("vote_user_id");

                entity.Property(e => e.VoteUserIp)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("vote_user_ip")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbPosts>(entity =>
            {
                entity.HasKey(e => e.PostId)
                    .HasName("PK_phpbb_posts_post_id");

                entity.ToTable("phpbb_posts");

                entity.HasIndex(e => e.ForumId, "forum_id");

                entity.HasIndex(e => new { e.PostId, e.TopicId, e.PosterId, e.PostTime }, "pid_post_time");

                entity.HasIndex(e => e.PostApproved, "post_approved");

                entity.HasIndex(e => e.PostUsername, "post_username");

                entity.HasIndex(e => e.PosterId, "poster_id");

                entity.HasIndex(e => e.PosterIp, "poster_ip");

                entity.HasIndex(e => new { e.TopicId, e.PostTime }, "tid_post_time");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.Property(e => e.PostId).HasColumnName("post_id");

                entity.Property(e => e.BbcodeBitfield)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("bbcode_bitfield")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BbcodeUid)
                    .IsRequired()
                    .HasMaxLength(8)
                    .HasColumnName("bbcode_uid")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.EnableBbcode)
                    .HasColumnName("enable_bbcode")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableMagicUrl)
                    .HasColumnName("enable_magic_url")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableSig)
                    .HasColumnName("enable_sig")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableSmilies)
                    .HasColumnName("enable_smilies")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.IconId).HasColumnName("icon_id");

                entity.Property(e => e.PostApproved)
                    .HasColumnName("post_approved")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PostAttachment).HasColumnName("post_attachment");

                entity.Property(e => e.PostChecksum)
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnName("post_checksum")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PostEditCount).HasColumnName("post_edit_count");

                entity.Property(e => e.PostEditLocked).HasColumnName("post_edit_locked");

                entity.Property(e => e.PostEditReason)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("post_edit_reason")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PostEditTime).HasColumnName("post_edit_time");

                entity.Property(e => e.PostEditUser).HasColumnName("post_edit_user");

                entity.Property(e => e.PostPostcount)
                    .HasColumnName("post_postcount")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PostReported).HasColumnName("post_reported");

                entity.Property(e => e.PostSubject)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("post_subject")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PostText)
                    .IsRequired()
                    .HasColumnName("post_text");

                entity.Property(e => e.PostTime).HasColumnName("post_time");

                entity.Property(e => e.PostUsername)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("post_username")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PosterId).HasColumnName("poster_id");

                entity.Property(e => e.PosterIp)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("poster_ip")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");
            });

            modelBuilder.Entity<PhpbbPrivmsgs>(entity =>
            {
                entity.HasKey(e => e.MsgId)
                    .HasName("PK_phpbb_privmsgs_msg_id");

                entity.ToTable("phpbb_privmsgs");

                entity.HasIndex(e => e.AuthorId, "author_id");

                entity.HasIndex(e => e.AuthorIp, "author_ip");

                entity.HasIndex(e => e.MessageTime, "message_time");

                entity.HasIndex(e => e.RootLevel, "root_level");

                entity.Property(e => e.MsgId).HasColumnName("msg_id");

                entity.Property(e => e.AuthorId).HasColumnName("author_id");

                entity.Property(e => e.AuthorIp)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("author_ip")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BbcodeBitfield)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("bbcode_bitfield")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BbcodeUid)
                    .IsRequired()
                    .HasMaxLength(8)
                    .HasColumnName("bbcode_uid")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.BccAddress)
                    .IsRequired()
                    .HasColumnName("bcc_address");

                entity.Property(e => e.EnableBbcode)
                    .HasColumnName("enable_bbcode")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableMagicUrl)
                    .HasColumnName("enable_magic_url")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableSig)
                    .HasColumnName("enable_sig")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.EnableSmilies)
                    .HasColumnName("enable_smilies")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IconId).HasColumnName("icon_id");

                entity.Property(e => e.MessageAttachment).HasColumnName("message_attachment");

                entity.Property(e => e.MessageEditCount).HasColumnName("message_edit_count");

                entity.Property(e => e.MessageEditReason)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("message_edit_reason")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.MessageEditTime).HasColumnName("message_edit_time");

                entity.Property(e => e.MessageEditUser).HasColumnName("message_edit_user");

                entity.Property(e => e.MessageReported).HasColumnName("message_reported");

                entity.Property(e => e.MessageSubject)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("message_subject")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.MessageText)
                    .IsRequired()
                    .HasColumnName("message_text");

                entity.Property(e => e.MessageTime).HasColumnName("message_time");

                entity.Property(e => e.RootLevel).HasColumnName("root_level");

                entity.Property(e => e.ToAddress)
                    .IsRequired()
                    .HasColumnName("to_address");
            });

            modelBuilder.Entity<PhpbbPrivmsgsTo>(entity =>
            {
                entity.ToTable("phpbb_privmsgs_to");

                entity.HasIndex(e => e.AuthorId, "author_id");

                entity.HasIndex(e => e.MsgId, "msg_id");

                entity.HasIndex(e => new { e.UserId, e.FolderId }, "usr_flder_id");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.AuthorId).HasColumnName("author_id");

                entity.Property(e => e.FolderId).HasColumnName("folder_id");

                entity.Property(e => e.MsgId).HasColumnName("msg_id");

                entity.Property(e => e.PmDeleted).HasColumnName("pm_deleted");

                entity.Property(e => e.PmForwarded).HasColumnName("pm_forwarded");

                entity.Property(e => e.PmMarked).HasColumnName("pm_marked");

                entity.Property(e => e.PmNew)
                    .HasColumnName("pm_new")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PmReplied).HasColumnName("pm_replied");

                entity.Property(e => e.PmUnread)
                    .HasColumnName("pm_unread")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<PhpbbRanks>(entity =>
            {
                entity.HasKey(e => e.RankId)
                    .HasName("PK_phpbb_ranks_rank_id");

                entity.ToTable("phpbb_ranks");

                entity.Property(e => e.RankId).HasColumnName("rank_id");

                entity.Property(e => e.RankImage)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("rank_image")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.RankMin).HasColumnName("rank_min");

                entity.Property(e => e.RankSpecial).HasColumnName("rank_special");

                entity.Property(e => e.RankTitle)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("rank_title")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbRecycleBin>(entity =>
            {
                entity.HasKey(e => new { e.Type, e.Id })
                    .HasName("PK_phpbb_recycle_bin_type");

                entity.ToTable("phpbb_recycle_bin");

                entity.Property(e => e.Type).HasColumnName("type");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Content).HasColumnName("content");

                entity.Property(e => e.DeleteTime).HasColumnName("delete_time");

                entity.Property(e => e.DeleteUser).HasColumnName("delete_user");
            });

            modelBuilder.Entity<PhpbbReports>(entity =>
            {
                entity.HasKey(e => e.ReportId)
                    .HasName("PK_phpbb_reports_report_id");

                entity.ToTable("phpbb_reports");

                entity.HasIndex(e => e.PmId, "pm_id");

                entity.HasIndex(e => e.PostId, "post_id");

                entity.Property(e => e.ReportId).HasColumnName("report_id");

                entity.Property(e => e.PmId).HasColumnName("pm_id");

                entity.Property(e => e.PostId).HasColumnName("post_id");

                entity.Property(e => e.ReasonId).HasColumnName("reason_id");

                entity.Property(e => e.ReportClosed).HasColumnName("report_closed");

                entity.Property(e => e.ReportText)
                    .IsRequired()
                    .HasColumnName("report_text");

                entity.Property(e => e.ReportTime).HasColumnName("report_time");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.UserNotify).HasColumnName("user_notify");
            });

            modelBuilder.Entity<PhpbbReportsReasons>(entity =>
            {
                entity.HasKey(e => e.ReasonId)
                    .HasName("PK_phpbb_reports_reasons_reason_id");

                entity.ToTable("phpbb_reports_reasons");

                entity.Property(e => e.ReasonId).HasColumnName("reason_id");

                entity.Property(e => e.ReasonDescription)
                    .IsRequired()
                    .HasColumnName("reason_description");

                entity.Property(e => e.ReasonOrder).HasColumnName("reason_order");

                entity.Property(e => e.ReasonTitle)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("reason_title")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbShortcuts>(entity =>
            {
                entity.HasKey(e => new { e.TopicId, e.ForumId })
                    .HasName("PK_phpbb_shortcuts_topic_id");

                entity.ToTable("phpbb_shortcuts");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");
            });

            modelBuilder.Entity<PhpbbSmilies>(entity =>
            {
                entity.HasKey(e => e.SmileyId)
                    .HasName("PK_phpbb_smilies_smiley_id");

                entity.ToTable("phpbb_smilies");

                entity.HasIndex(e => e.DisplayOnPosting, "display_on_post");

                entity.Property(e => e.SmileyId).HasColumnName("smiley_id");

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnName("code")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.DisplayOnPosting)
                    .HasColumnName("display_on_posting")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.Emotion)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnName("emotion")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.SmileyHeight).HasColumnName("smiley_height");

                entity.Property(e => e.SmileyOrder).HasColumnName("smiley_order");

                entity.Property(e => e.SmileyUrl)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnName("smiley_url")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.SmileyWidth).HasColumnName("smiley_width");
            });

            modelBuilder.Entity<PhpbbStyles>(entity =>
            {
                entity.HasKey(e => e.StyleId)
                    .HasName("PK_phpbb_styles_style_id");

                entity.ToTable("phpbb_styles");

                entity.HasIndex(e => e.ImagesetId, "imageset_id");

                entity.HasIndex(e => e.StyleName, "phpbb_styles$style_name")
                    .IsUnique();

                entity.HasIndex(e => e.TemplateId, "template_id");

                entity.HasIndex(e => e.ThemeId, "theme_id");

                entity.Property(e => e.StyleId).HasColumnName("style_id");

                entity.Property(e => e.ImagesetId).HasColumnName("imageset_id");

                entity.Property(e => e.StyleActive)
                    .HasColumnName("style_active")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.StyleCopyright)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("style_copyright")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.StyleName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("style_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TemplateId).HasColumnName("template_id");

                entity.Property(e => e.ThemeId).HasColumnName("theme_id");
            });

            modelBuilder.Entity<PhpbbTopics>(entity =>
            {
                entity.HasKey(e => e.TopicId)
                    .HasName("PK_phpbb_topics_topic_id");

                entity.ToTable("phpbb_topics");

                entity.HasIndex(e => new { e.ForumId, e.TopicLastPostTime, e.TopicMovedId }, "fid_time_moved");

                entity.HasIndex(e => new { e.ForumId, e.TopicApproved, e.TopicLastPostId }, "forum_appr_last");

                entity.HasIndex(e => e.ForumId, "forum_id");

                entity.HasIndex(e => new { e.ForumId, e.TopicType }, "forum_id_type");

                entity.HasIndex(e => e.TopicLastPostTime, "last_post_time");

                entity.HasIndex(e => e.TopicApproved, "topic_approved");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.IconId).HasColumnName("icon_id");

                entity.Property(e => e.PollLastVote).HasColumnName("poll_last_vote");

                entity.Property(e => e.PollLength).HasColumnName("poll_length");

                entity.Property(e => e.PollMaxOptions)
                    .HasColumnName("poll_max_options")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PollStart).HasColumnName("poll_start");

                entity.Property(e => e.PollTitle)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("poll_title")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.PollVoteChange).HasColumnName("poll_vote_change");

                entity.Property(e => e.TopicApproved)
                    .HasColumnName("topic_approved")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.TopicAttachment).HasColumnName("topic_attachment");

                entity.Property(e => e.TopicBumped).HasColumnName("topic_bumped");

                entity.Property(e => e.TopicBumper).HasColumnName("topic_bumper");

                entity.Property(e => e.TopicFirstPostId).HasColumnName("topic_first_post_id");

                entity.Property(e => e.TopicFirstPosterColour)
                    .IsRequired()
                    .HasMaxLength(6)
                    .HasColumnName("topic_first_poster_colour")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicFirstPosterName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("topic_first_poster_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicLastPostId).HasColumnName("topic_last_post_id");

                entity.Property(e => e.TopicLastPostSubject)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("topic_last_post_subject")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicLastPostTime).HasColumnName("topic_last_post_time");

                entity.Property(e => e.TopicLastPosterColour)
                    .IsRequired()
                    .HasMaxLength(6)
                    .HasColumnName("topic_last_poster_colour")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicLastPosterId).HasColumnName("topic_last_poster_id");

                entity.Property(e => e.TopicLastPosterName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("topic_last_poster_name")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicLastViewTime).HasColumnName("topic_last_view_time");

                entity.Property(e => e.TopicMovedId).HasColumnName("topic_moved_id");

                entity.Property(e => e.TopicPoster).HasColumnName("topic_poster");

                entity.Property(e => e.TopicReplies).HasColumnName("topic_replies");

                entity.Property(e => e.TopicRepliesReal).HasColumnName("topic_replies_real");

                entity.Property(e => e.TopicReported).HasColumnName("topic_reported");

                entity.Property(e => e.TopicStatus).HasColumnName("topic_status");

                entity.Property(e => e.TopicTime).HasColumnName("topic_time");

                entity.Property(e => e.TopicTimeLimit).HasColumnName("topic_time_limit");

                entity.Property(e => e.TopicTitle)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("topic_title")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.TopicType).HasColumnName("topic_type");

                entity.Property(e => e.TopicViews).HasColumnName("topic_views");
            });

            modelBuilder.Entity<PhpbbTopicsTrack>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.TopicId })
                    .HasName("PK_phpbb_topics_track_user_id");

                entity.ToTable("phpbb_topics_track");

                entity.HasIndex(e => e.ForumId, "forum_id");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.ForumId).HasColumnName("forum_id");

                entity.Property(e => e.MarkTime).HasColumnName("mark_time");
            });

            modelBuilder.Entity<PhpbbTopicsWatch>(entity =>
            {
                entity.HasKey(e => new { e.TopicId, e.UserId })
                    .HasName("PK_phpbb_topics_watch_topic_id");

                entity.ToTable("phpbb_topics_watch");

                entity.HasIndex(e => e.NotifyStatus, "notify_stat");

                entity.HasIndex(e => e.TopicId, "topic_id");

                entity.HasIndex(e => e.UserId, "user_id");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.NotifyStatus).HasColumnName("notify_status");
            });

            modelBuilder.Entity<PhpbbUserGroup>(entity =>
            {
                entity.HasKey(e => new { e.GroupId, e.UserId })
                    .HasName("PK_phpbb_user_group_group_id");

                entity.ToTable("phpbb_user_group");

                entity.HasIndex(e => e.GroupId, "group_id");

                entity.HasIndex(e => e.GroupLeader, "group_leader");

                entity.HasIndex(e => e.UserId, "user_id");

                entity.Property(e => e.GroupId).HasColumnName("group_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.GroupLeader).HasColumnName("group_leader");

                entity.Property(e => e.UserPending)
                    .HasColumnName("user_pending")
                    .HasDefaultValueSql("((1))");
            });

            modelBuilder.Entity<PhpbbUserTopicPostNumber>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.TopicId })
                    .HasName("PK_phpbb_user_topic_post_number_user_id");

                entity.ToTable("phpbb_user_topic_post_number");

                entity.HasIndex(e => new { e.UserId, e.TopicId }, "user_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.TopicId).HasColumnName("topic_id");

                entity.Property(e => e.PostNo).HasColumnName("post_no");
            });

            modelBuilder.Entity<PhpbbUsers>(entity =>
            {
                entity.HasKey(e => e.UserId)
                    .HasName("PK_phpbb_users_user_id");

                entity.ToTable("phpbb_users");

                entity.HasIndex(e => e.UsernameClean, "phpbb_users$username_clean")
                    .IsUnique();

                entity.HasIndex(e => e.UserBirthday, "user_birthday");

                entity.HasIndex(e => e.UserEmailHash, "user_email_hash");

                entity.HasIndex(e => e.UserType, "user_type");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.GroupId)
                    .HasColumnName("group_id")
                    .HasDefaultValueSql("((3))");

                entity.Property(e => e.JumpToUnread)
				    .HasColumnType("smallint")
                    .HasConversion(new BoolToZeroOneConverter<Int16>())
					.HasColumnName("jump_to_unread")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserActkey)
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnName("user_actkey")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserAim)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_aim")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserAllowMassemail)
                    .HasColumnName("user_allow_massemail")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserAllowPm)
                    .HasColumnName("user_allow_pm")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserAllowViewemail).HasColumnName("user_allow_viewemail");

                entity.Property(e => e.UserAllowViewonline)
                    .HasColumnName("user_allow_viewonline")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserAvatar)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_avatar")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserAvatarHeight)
                    .HasConversion(new IntToShortValueConverter())
                    .HasColumnName("user_avatar_height");

                entity.Property(e => e.UserAvatarType)
				    .HasConversion(new ByteToShortValueConverter())
				    .HasColumnName("user_avatar_type");

                entity.Property(e => e.UserAvatarWidth)
                    .HasConversion(new IntToShortValueConverter())
                    .HasColumnName("user_avatar_width");

                entity.Property(e => e.UserBirthday)
                    .IsRequired()
                    .HasMaxLength(10)
                    .HasColumnName("user_birthday")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserColour)
                    .IsRequired()
                    .HasMaxLength(6)
                    .HasColumnName("user_colour")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserDateformat)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasColumnName("user_dateformat")
                    .HasDefaultValueSql("(N'dddd, dd.MM.yyyy, HH:mm')");

                entity.Property(e => e.UserDst).HasColumnName("user_dst");

                entity.Property(e => e.UserEditTime)
                    .HasConversion(new LongToIntValueConverter())
                    .HasColumnName("user_edit_time")
                    .HasDefaultValueSql("((60))");

                entity.Property(e => e.UserEmail)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("user_email")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserEmailHash).HasColumnName("user_email_hash");

                entity.Property(e => e.UserEmailtime).HasColumnName("user_emailtime");

                entity.Property(e => e.UserFormSalt)
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnName("user_form_salt")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserFrom)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("user_from")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserFullFolder)
                    .HasColumnName("user_full_folder")
                    .HasDefaultValueSql("((-3))");

                entity.Property(e => e.UserIcq)
                    .IsRequired()
                    .HasMaxLength(15)
                    .HasColumnName("user_icq")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserInactiveReason)
                    //.HasConversion(new UserInactiveReasonToByteValueConverter())
                    .HasColumnName("user_inactive_reason");

                entity.Property(e => e.UserInactiveTime).HasColumnName("user_inactive_time");

                entity.Property(e => e.UserInterests)
                    .IsRequired()
                    .HasColumnName("user_interests");

                entity.Property(e => e.UserIp)
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnName("user_ip")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserJabber)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_jabber")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserLang)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasColumnName("user_lang")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserLastConfirmKey)
                    .IsRequired()
                    .HasMaxLength(10)
                    .HasColumnName("user_last_confirm_key")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserLastPrivmsg).HasColumnName("user_last_privmsg");

                entity.Property(e => e.UserLastSearch).HasColumnName("user_last_search");

                entity.Property(e => e.UserLastWarning).HasColumnName("user_last_warning");

                entity.Property(e => e.UserLastmark).HasColumnName("user_lastmark");

                entity.Property(e => e.UserLastpage)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("user_lastpage")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserLastpostTime).HasColumnName("user_lastpost_time");

                entity.Property(e => e.UserLastvisit).HasColumnName("user_lastvisit");

                entity.Property(e => e.UserLoginAttempts)
				    .HasConversion(new ByteToShortValueConverter())
                    .HasColumnName("user_login_attempts");

                entity.Property(e => e.UserMessageRules).HasColumnName("user_message_rules");

                entity.Property(e => e.UserMsnm)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_msnm")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserNew)
                    .HasColumnName("user_new")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserNewPrivmsg).HasColumnName("user_new_privmsg");

                entity.Property(e => e.UserNewpasswd)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_newpasswd")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserNotify).HasColumnName("user_notify");

                entity.Property(e => e.UserNotifyPm)
                    .HasColumnName("user_notify_pm")
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UserNotifyType)
				    .HasConversion(new ByteToShortValueConverter())
				    .HasColumnName("user_notify_type");

                entity.Property(e => e.UserOcc)
                    .IsRequired()
                    .HasColumnName("user_occ");

                entity.Property(e => e.UserOptions)
                    .HasConversion(new LongToIntValueConverter())
                    .HasColumnName("user_options")
                    .HasDefaultValueSql("((230271))");

                entity.Property(e => e.UserPassConvert).HasColumnName("user_pass_convert");

                entity.Property(e => e.UserPasschg).HasColumnName("user_passchg");

                entity.Property(e => e.UserPassword)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_password")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserPermFrom).HasColumnName("user_perm_from");

                entity.Property(e => e.UserPermissions)
                    .IsRequired()
                    .HasColumnName("user_permissions");

                entity.Property(e => e.UserPostShowDays)
                    .HasConversion(new IntToShortValueConverter())
                    .HasColumnName("user_post_show_days");

                entity.Property(e => e.UserPostSortbyDir)
                    .IsRequired()
                    .HasMaxLength(1)
                    .HasColumnName("user_post_sortby_dir")
                    .HasDefaultValueSql("(N'a')")
                    .IsFixedLength();

                entity.Property(e => e.UserPostSortbyType)
                    .IsRequired()
                    .HasMaxLength(1)
                    .HasColumnName("user_post_sortby_type")
                    .HasDefaultValueSql("(N't')")
                    .IsFixedLength();

                entity.Property(e => e.UserPosts).HasColumnName("user_posts");

                entity.Property(e => e.UserRank).HasColumnName("user_rank");

                entity.Property(e => e.UserRegdate).HasColumnName("user_regdate");

                entity.Property(e => e.UserReminded)
				    .HasConversion(new ByteToShortValueConverter())
				    .HasColumnName("user_reminded");

                entity.Property(e => e.UserRemindedTime).HasColumnName("user_reminded_time");

                entity.Property(e => e.UserShouldSignIn)
				    .HasConversion(new BoolToZeroOneConverter<short>())
					.HasColumnType("smallint")
                    .HasColumnName("user_should_sign_in");

                entity.Property(e => e.UserSig)
                    .IsRequired()
                    .HasColumnName("user_sig");

                entity.Property(e => e.UserSigBbcodeBitfield)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_sig_bbcode_bitfield")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserSigBbcodeUid)
                    .IsRequired()
                    .HasMaxLength(8)
                    .HasColumnName("user_sig_bbcode_uid")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserStyle).HasColumnName("user_style");

                entity.Property(e => e.UserTimezone)
                    .HasColumnType("decimal(5, 2)")
                    .HasColumnName("user_timezone")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.UserTopicShowDays)
                    .HasConversion(new IntToShortValueConverter())
                    .HasColumnName("user_topic_show_days");

                entity.Property(e => e.UserTopicSortbyDir)
                    .IsRequired()
                    .HasMaxLength(1)
                    .HasColumnName("user_topic_sortby_dir")
                    .HasDefaultValueSql("(N'd')")
                    .IsFixedLength();

                entity.Property(e => e.UserTopicSortbyType)
                    .IsRequired()
                    .HasMaxLength(1)
                    .HasColumnName("user_topic_sortby_type")
                    .HasDefaultValueSql("(N't')")
                    .IsFixedLength();

                entity.Property(e => e.UserType)
                    .HasConversion(new ByteToShortValueConverter())
                    .HasColumnName("user_type");

                entity.Property(e => e.UserUnreadPrivmsg).HasColumnName("user_unread_privmsg");

                entity.Property(e => e.UserWarnings)
				    .HasConversion(new ByteToShortValueConverter())
                    .HasColumnName("user_warnings");

                entity.Property(e => e.UserWebsite)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("user_website")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UserYim)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("user_yim")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("username")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.UsernameClean)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("username_clean")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbWords>(entity =>
            {
                entity.HasKey(e => e.WordId)
                    .HasName("PK_phpbb_words_word_id");

                entity.ToTable("phpbb_words");

                entity.Property(e => e.WordId).HasColumnName("word_id");

                entity.Property(e => e.Replacement)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("replacement")
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.Word)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("word")
                    .HasDefaultValueSql("(N'')");
            });

            modelBuilder.Entity<PhpbbZebra>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.ZebraId })
                    .HasName("PK_phpbb_zebra_user_id");

                entity.ToTable("phpbb_zebra");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.ZebraId).HasColumnName("zebra_id");

                entity.Property(e => e.Foe).HasColumnName("foe");

                entity.Property(e => e.Friend).HasColumnName("friend");
            });

        }

		class ByteToShortValueConverter : ValueConverter<byte, short>
		{
			public ByteToShortValueConverter() : base(@byte => Convert.ToInt16(@byte), @short => Convert.ToByte(@short),  null)
			{
			}
		}

		//class UserInactiveReasonToByteValueConverter : ValueConverter<UserInactiveReason, byte>
		//{
		//	public UserInactiveReasonToByteValueConverter() : base(userInactiveReason => Convert.ToByte(userInactiveReason), @byte => (UserInactiveReason)Enum.ToObject(typeof(UserInactiveReason), @byte), null)
		//	{
		//	}
		//}

		class IntToShortValueConverter : ValueConverter<short, int>
		{
            public IntToShortValueConverter() : base(@short => Convert.ToInt32(@short), @int => Convert.ToInt16(@int), null)
            {
            }
		}

		class LongToIntValueConverter : ValueConverter<int, long>
		{
			public LongToIntValueConverter() : base(@int => Convert.ToInt64(@int), @long => Convert.ToInt32(@long), null)
			{
			}
		}
	}
}