using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ModeratorModel : AuthenticatedPageModel
    {
        private readonly IModeratorService _moderatorService;
        private readonly IPostService _postService;
        private readonly IOperationLogService _operationLogService;
        private readonly ILogger _logger;

        [BindProperty(SupportsGet = true)]
        public ModeratorPanelMode Mode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty]
        public int[]? SelectedReports { get; set; }

        [BindProperty]
        public int[]? SelectedTopics { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedTopicIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        [BindProperty]
        public string[]? SelectedDeletedItems { get; set; }

        public string? ForumName { get; private set; }
        public List<TopicGroup>? Topics { get; private set; }
        public List<ReportDto>? Reports { get; private set; }
        public string? MessageClass { get; private set; }
        public string? Message { get; private set; }
        public bool ScrollToAction => TopicAction.HasValue && DestinationForumId.HasValue;
        public IEnumerable<DeletedItemGroup>? DeletedItems { get; private set; }

        public ModeratorModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter,
            ITranslationProvider translationProvider, IModeratorService moderatorService, IPostService postService,
            IOperationLogService operationLogService, ILogger logger, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _moderatorService = moderatorService;
            _postService = postService;
            _operationLogService = operationLogService;
            _logger = logger;
        }

        public int[] GetTopicIds()
        {
            if (SelectedTopics?.Any() ?? false)
            {
                return SelectedTopics;
            }
            return SelectedTopicIds?.Split(',')?.Select(x => int.TryParse(x, out var val) ? val : 0)?.Where(x => x != 0)?.ToArray() ?? Array.Empty<int>();
        }

        public async Task<IActionResult> OnGet()
            => await WithModerator(ForumId, async () =>
            {
                ForumName = await SqlExecuter.QueryFirstOrDefaultAsync<string?>("SELECT forum_name FROM phpbb_forums WHERE forum_id = @forumId", new { ForumId });
                Topics = await ForumService.GetTopicGroups(ForumId);
                Reports = await _moderatorService.GetReportedMessages(0);

                var allItems = await SqlExecuter.QueryAsync<DeletedItemDto>(
                    @"SELECT rb.id, rb.delete_user, coalesce(u.username, @anonymous) AS delete_user_name, delete_time, content AS raw_content, type
                        FROM phpbb_recycle_bin rb
                        LEFT JOIN phpbb_users u ON rb.delete_user = u.user_id
                        ORDER BY rb.delete_time DESC",
                    new { anonymous = TranslationProvider.BasicText[Language, "ANONYMOUS", Casing.None] });

                await Task.WhenAll(allItems.Select(async i => i.Type switch
                {
                    RecycleBinItemType.Forum => i.Value = await CompressionUtility.DecompressObject<ForumDto>(i.RawContent),
                    RecycleBinItemType.Topic => i.Value = await CompressionUtility.DecompressObject<TopicDto>(i.RawContent),
                    RecycleBinItemType.Post => i.Value = await CompressionUtility.DecompressObject<PostDto>(i.RawContent),
                    _ => i.Value = Task.FromResult<object?>(null)
                }));

                DeletedItems = from i in allItems
                               orderby i.Type
                               group i by i.Type into groups
                               select new DeletedItemGroup
                               {
                                   Type = groups.Key,
                                   Items = groups
                               };

                return Page();

            });

        public async Task<IActionResult> OnPostCloseReports()
            => await WithModerator(ForumId, async () =>
            {
                var lang = Language;
                if (SelectedReports?.Any() != true)
                {
                    MessageClass = "message warning";
                    Message = TranslationProvider.Moderator[lang, "NO_REPORTS_SELECTED"];
                }
                else
                {
                    try
                    {
                        await SqlExecuter.ExecuteAsync(
                            "UPDATE phpbb_reports SET report_closed = 1 WHERE report_id IN @SelectedReports",
                            new { SelectedReports }
                        );
                        MessageClass = "message success";
                        Message = TranslationProvider.Moderator[lang, "REPORTS_CLOSED_SUCCESSFULLY"];
                    }
                    catch (Exception ex)
                    {
                        var id = _logger.ErrorWithId(ex);
                        MessageClass = "message fail";
                        Message = string.Format(TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                    }
                }
                return await OnGet();
            });

        public async Task<IActionResult> OnPostManageTopics()
            => await WithModerator(ForumId, async () =>
            {
                var lang = Language;
                var logDto = new OperationLogDto
                {
                    Action = TopicAction,
                    UserId = ForumUser.UserId
                };

                try
                {
                    var results = new List<(string Message, bool? IsSuccess)>();
                    foreach(var topicId in GetTopicIds())
                    {
                        results.Add(TopicAction switch
                        {
                            ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(topicId, TopicType.Normal, logDto),
                            ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(topicId, TopicType.Important, logDto),
                            ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(topicId, TopicType.Announcement, logDto),
                            ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(topicId, TopicType.Global, logDto),
                            ModeratorTopicActions.MoveTopic => await _moderatorService.MoveTopic(topicId, DestinationForumId ?? 0, logDto),
                            ModeratorTopicActions.LockTopic => await _moderatorService.LockUnlockTopic(topicId, true, logDto),
                            ModeratorTopicActions.UnlockTopic => await _moderatorService.LockUnlockTopic(topicId, false, logDto),
                            ModeratorTopicActions.CreateShortcut => await _moderatorService.CreateShortcut(topicId, DestinationForumId ?? 0, logDto),
                            ModeratorTopicActions.RemoveShortcut => await _moderatorService.RemoveShortcut(topicId, ForumId, logDto),
                            _ => throw new NotSupportedException($"Can't perform action '{TopicAction}'")
                        });
                    }

                    if (results.All(r => r.IsSuccess ?? false))
                    {
                        MessageClass = "message success";
                        Message = string.Format(TranslationProvider.Moderator[lang, "MODERATOR_TOPIC_ACTION_SUCCESSFUL_FORMAT"], TopicAction);
                    }
                    else if (results.All(r => !(r.IsSuccess ?? false)))
                    {
                        throw new AggregateException(results.Select(r => new Exception(r.Message)));
                    }
                    else
                    {
                        var failed = results.Where(r => !(r.IsSuccess ?? false));
                        var id = _logger.WarningWithId(new AggregateException(failed.Select(r => new Exception(r.Message))));
                        MessageClass = "message warning";
                        Message = string.Format(TranslationProvider.Moderator[lang, "MODERATOR_ACTION_PARTIAL_FAILED_FORMAT"], id);
                    }
                }
                catch (Exception ex)
                {
                    var id = _logger.ErrorWithId(ex);
                    MessageClass = "message fail";
                    Message = string.Format(TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                }

                TopicAction = null;
                DestinationForumId = null;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRestoreDeletedItems()
            => await WithModerator(0, async () =>
            {
                var lang = Language;
                try
                {
                    var itemGroups = from i in SelectedDeletedItems!
                                     
                                     let elements = i.Split('/')
                                     let type = Enum.Parse<RecycleBinItemType>(elements[0])
                                     let id = int.Parse(elements[1])
                                     
                                     group id by type into groups
                                     orderby groups.Key descending //posts, topics, forums in this order
                                     
                                     select groups;

                    if (!await UserService.IsAdmin(ForumUser) && itemGroups.Any(item => item.Key == RecycleBinItemType.Forum))
                    {
                        MessageClass = "message fail";
                        Message = string.Format(TranslationProvider.Errors[lang, "MISSING_REQUIRED_PERMISSIONS"]);
                    }
                    else
                    {
                        //async not supported here
                        var results = new List<bool>(SelectedDeletedItems!.Length);
                        foreach (var group in itemGroups)
                        {
                            foreach (var id in group)
                            {
                                results.Add(await RestoreAny(id, group.Key));
                            }
                        }

                        if (results.All(r => r))
                        {
                            MessageClass = "message success";
                            Message = TranslationProvider.Moderator[lang, "ITEMS_RESTORED_SUCCESSFULLY"];
                        }
                        else if (results.All(r => !r))
                        {
                            MessageClass = "message fail";
                            Message = TranslationProvider.Moderator[lang, "ITEMS_RESTORATION_FAILED"];
                        }
                        else
                        {
                            MessageClass = "message fail";
                            Message = TranslationProvider.Moderator[lang, "ITEMS_RESTORATION_FAILED_PARTIALLY"];
                        }
                    }
                }
                catch (Exception ex)
                {
                    var id = _logger.ErrorWithId(ex);
                    MessageClass = "message fail";
                    Message = string.Format(TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                }

                TopicAction = null;
                DestinationForumId = null;
               
                return await OnGet();
            });

        private async Task<bool> RestoreForum(int forumId)
        {
            var deletedItem = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbRecycleBin>(
                "SELECT * from phpbb_recycle_bin WHERE type = @forumType AND id = @forumId",
                new
                {
                    forumType = RecycleBinItemType.Forum,
                    ForumId
                });
            if (deletedItem == null)
            {
                return false;
            }
            var dto = await CompressionUtility.DecompressObject<ForumDto>(deletedItem.Content);
            var toAdd = new PhpbbForums
            {
                ForumId = dto!.ForumId!.Value,
                ForumName = dto.ForumName!,
                ForumDesc = dto.ForumDesc!,
                ForumPassword = dto.ForumPassword!,
                ParentId = dto.ParentId!.Value,
                ForumType = dto.ForumType!.Value,
                ForumRules = dto.ForumRules!,
                ForumRulesLink = dto.ForumRulesLink!,
                LeftId = dto.LeftId,
                ForumLastPostId = dto.ForumLastPostId,
                ForumLastPosterId = dto.ForumLastPosterId,
                ForumLastPostSubject = dto.ForumLastPostSubject,
                ForumLastPostTime = dto.ForumLastPostTime,
                ForumLastPosterName = dto.ForumLastPosterName,
                ForumLastPosterColour = dto.ForumLastPosterColour
            };

            var param = new DynamicParameters(toAdd);
            param.AddDynamicParams(new
            {
                deleteForumType = RecycleBinItemType.Forum,
                deleteForumId = ForumId
            });
            await SqlExecuter.ExecuteAsync(
                    @$"INSERT INTO phpbb_forums 
                       VALUES (@ForumId, @ParentId, @LeftId, @RightId, @ForumParents, @ForumName, @ForumDesc, @ForumDescBitfield, @ForumDescOptions, @ForumDescUid, @ForumLink, @ForumPassword, @ForumStyle, @ForumImage, @ForumRules, @ForumRulesLink, @ForumRulesBitfield, @ForumRulesOptions, @ForumRulesUid, @ForumTopicsPerPage, @ForumType, @ForumStatus, @ForumPosts, @ForumTopics, @ForumTopicsReal, @ForumLastPostId, @ForumLastPosterId, @ForumLastPostSubject, @ForumLastPostTime, @ForumLastPosterName, @ForumLastPosterColour, @ForumFlags, @ForumOptions, @DisplaySubforumList, @DisplayOnIndex, @EnableIndexing, @EnableIcons, @EnablePrune, @PruneNext, @PruneDays, @PruneViewed, @PruneFreq, @ForumEditTime);
                       DELETE FROM phpbb_recycle_bin WHERE type = @deleteForumType AND id = @deleteForumId;",
                    param);


            await _operationLogService.LogAdminForumAction(AdminForumActions.Restore, ForumUser.UserId, toAdd);

            return true;
        }

        private async Task<bool> RestoreTopic(int topicId)
        {
            var deletedItem = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbRecycleBin>(
                "SELECT * from phpbb_recycle_bin WHERE type = @topicType AND id = @topicId",
                new
                {
                    topicType = RecycleBinItemType.Topic,
                    topicId
                });
            if (deletedItem == null)
            {
                return false;
            }
            var dto = await CompressionUtility.DecompressObject<TopicDto>(deletedItem.Content);

            if (!await ParentForumExists())
            {
                if (!await RestoreForum(dto!.ForumId!.Value))
                {
                    return false;
                }
                if (!await ParentForumExists())
                {
                    return false;
                }
            }
            var toAdd = new PhpbbTopics
            {
                ForumId = dto!.ForumId!.Value,
                TopicId = dto.TopicId!.Value,
                TopicTitle = dto.TopicTitle!,
                TopicStatus = dto.TopicStatus,
                TopicType = dto.TopicType!.Value,
                TopicLastPosterColour = dto.TopicLastPosterColour!,
                TopicLastPosterId = dto.TopicLastPosterId!.Value,
                TopicLastPosterName = dto.TopicLastPosterName!,
                TopicLastPostTime = dto.TopicLastPostTime!.Value,
                TopicLastPostId = dto.TopicLastPostId!.Value
            };
            if (dto.Poll != null)
            {
                toAdd.PollTitle = dto.Poll.PollTitle!;
                toAdd.PollStart = dto.Poll.PollStart.ToUnixTimestamp();
                toAdd.PollLength = dto.Poll.PollDurationSecons;
                toAdd.PollMaxOptions = (byte)dto.Poll.PollMaxOptions;
                toAdd.PollVoteChange = dto.Poll.VoteCanBeChanged.ToByte();

                await SqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_poll_options (poll_option_id, poll_option_text, topic_id) " +
                    "VALUES (@pollOptionId, @pollOptionText, @topicId)", 
                    dto.Poll.PollOptions);
            }

			var param = new DynamicParameters(toAdd);
			param.AddDynamicParams(new
			{
				deleteTopicType = RecycleBinItemType.Topic,
				deleteTopicId = topicId
			});
			var newTopicId = await SqlExecuter.ExecuteScalarAsync<int>(
                    @$"INSERT INTO phpbb_topics(forum_id, icon_id, topic_attachment, topic_approved, topic_reported, topic_title, topic_poster, topic_time, topic_time_limit, topic_views, topic_replies, topic_replies_real, topic_status, topic_type, topic_first_post_id, topic_first_poster_name, topic_first_poster_colour, topic_last_post_id, topic_last_poster_id, topic_last_poster_name, topic_last_poster_colour, topic_last_post_subject, topic_last_post_time, topic_last_view_time, topic_moved_id, topic_bumped, topic_bumper, poll_title, poll_start, poll_length, poll_max_options, poll_last_vote, poll_vote_change)
                       VALUES (@ForumId, @IconId, @TopicAttachment, @TopicApproved, @TopicReported, @TopicTitle, @TopicPoster, @TopicTime, @TopicTimeLimit, @TopicViews, @TopicReplies, @TopicRepliesReal, @TopicStatus, @TopicType, @TopicFirstPostId, @TopicFirstPosterName, @TopicFirstPosterColour, @TopicLastPostId, @TopicLastPosterId, @TopicLastPosterName, @TopicLastPosterColour, @TopicLastPostSubject, @TopicLastPostTime, @TopicLastViewTime, @TopicMovedId, @TopicBumped, @TopicBumper, @PollTitle, @PollStart, @PollLength, @PollMaxOptions, @PollLastVote, @PollVoteChange);
                       SELECT {SqlExecuter.LastInsertedItemId};
                       DELETE FROM phpbb_recycle_bin WHERE type = @deleteTopicType AND id = @deleteTopicId",
                    param);

            var deletedPosts = await SqlExecuter.QueryAsync<PhpbbRecycleBin>(
                "SELECT * FROM phpbb_recycle_bin WHERE type = @postType",
                new { postType = RecycleBinItemType.Post });               
            var posts = await Task.WhenAll(deletedPosts.Select(dp => CompressionUtility.DecompressObject<PostDto>(dp.Content)));
            foreach (var post in posts.Where(p => p!.TopicId == topicId))
            {
                await RestorePost(post!.PostId, newTopicId);
            }

            await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.RestoreTopic, ForumUser.UserId, toAdd.TopicId, $"Old topic id: {newTopicId}");

            return true;

            async Task<bool> ParentForumExists()
            {
                var result = await SqlExecuter.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_forums WHERE forum_id = @forumId",
                    new { forumId = dto?.ForumId ?? 0 });
                return result == 1;
            }
        }

        private async Task<bool> RestorePost(int postId, int? newTopicId = null)
        {
            using var transaction = SqlExecuter.BeginTransaction();
            var deletedItem = await transaction.QueryFirstOrDefaultAsync<PhpbbRecycleBin>(
                "SELECT * from phpbb_recycle_bin WHERE type = @postType AND id = @postId",
                new
                {
                    postType = RecycleBinItemType.Post,
                    postId
                });
            if (deletedItem == null)
            {
                return false;
            }
            var dto = await CompressionUtility.DecompressObject<PostDto>(deletedItem.Content);
            if (!await ParentTopicExists())
            {
                if (!await RestoreTopic(dto!.TopicId))
                {
                    return false;
                }

                if (!await ParentTopicExists())
                {
                    return false;
                }
            }

            var toAdd = await transaction.QueryFirstOrDefaultAsync<PhpbbPosts>(
				$@"INSERT INTO phpbb_posts(topic_id, forum_id, poster_id, icon_id, poster_ip, post_time, post_approved, post_reported, enable_bbcode, enable_smilies, enable_magic_url, enable_sig, post_username, post_subject, post_text, post_checksum, post_attachment, bbcode_bitfield, bbcode_uid, post_postcount, post_edit_time, post_edit_reason, post_edit_user, post_edit_count, post_edit_locked)
                   VALUES (@TopicId, @ForumId, @PosterId, @IconId, @PosterIp, @PostTime, @PostApproved, @PostReported, @EnableBbcode, @EnableSmilies, @EnableMagicUrl, @EnableSig, @PostUsername, @PostSubject, @PostText, @PostChecksum, @PostAttachment, @BbcodeBitfield, @BbcodeUid, @PostPostcount, @PostEditTime, @PostEditReason, @PostEditUser, @PostEditCount, @PostEditLocked);
                   SELECT * FROM phpbb_posts WHERE post_id = {SqlExecuter.LastInsertedItemId};",
				new PhpbbPosts
				{
					PosterId = dto!.AuthorId,
					PostUsername = dto.AuthorName!,
					BbcodeUid = dto.BbcodeUid!,
					ForumId = dto.ForumId,
					TopicId = newTopicId ?? dto.TopicId,
					PostId = dto.PostId,
					PostTime = dto.PostTime,
					PostSubject = dto.PostSubject!,
					PostText = dto.PostText!
				});


            if (dto.Attachments?.Any() ?? false)
            {
                foreach(var a in dto.Attachments.Select(a => new PhpbbAttachments
                {
                    PostMsgId = toAdd.PostId,
                    PosterId = dto.AuthorId,
                    RealFilename = a.DisplayName!,
                    AttachComment = a.Comment!,
                    AttachId = a.Id,
                    Mimetype = a.MimeType!,
                    DownloadCount = a.DownloadCount,
                    Filesize = a.FileSize,
                    PhysicalFilename = a.PhysicalFileName!,
                    IsOrphan = 0
                }))
                {
                    await transaction.ExecuteAsync(
						@"INSERT INTO phpbb_attachments(post_msg_id, topic_id, in_message, poster_id, is_orphan, physical_filename, real_filename, download_count, attach_comment, extension, mimetype, filesize, filetime, thumbnail)
                          VALUES (@PostMsgId, @TopicId, @InMessage, @PosterId, @IsOrphan, @PhysicalFilename, @RealFilename, @DownloadCount, @AttachComment, @Extension, @Mimetype, @Filesize, @Filetime, @Thumbnail);",
                        a);
                }
            }

            await _moderatorService.CascadePostAdd(toAdd, false, transaction);

            await _operationLogService.LogModeratorPostAction(ModeratorPostActions.RestorePosts, ForumUser.UserId, toAdd, $"<a href=\"{ForumLinkUtility.GetRelativeUrlToPost(toAdd.PostId)}\" target=\"_blank\">LINK</a><br/>Old post id: {dto.PostId}");

            await transaction.ExecuteAsync(
				"DELETE FROM phpbb_recycle_bin WHERE type = @postType AND id = @postId",
				new
				{
					postType = RecycleBinItemType.Post,
					postId
				});

			return true;

            async Task<bool> ParentTopicExists()
            {
                var result = await transaction.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_topics WHERE topic_id = @topicId",
                    new { topicId = dto?.TopicId ?? 0 });
                return result == 1;
            }
        }

        Task<bool> RestoreAny(int itemId, RecycleBinItemType itemType) => itemType switch
        {
            RecycleBinItemType.Forum => RestoreForum(itemId),
            RecycleBinItemType.Topic => RestoreTopic(itemId),
            RecycleBinItemType.Post => RestorePost(itemId),
            _ => throw new NotSupportedException($"Can't restore item of type '{itemType}'.")
        };
    }
}
