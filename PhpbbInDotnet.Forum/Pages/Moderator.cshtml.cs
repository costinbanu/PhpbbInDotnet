using Microsoft.AspNetCore.Mvc;
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
                await Task.WhenAll(SetForumName(), SetTopics(), SetReports(), SetDeletedItems());
                return Page();

                async Task SetForumName()
                    => ForumName = await SqlExecuter.QueryFirstOrDefaultAsync<string?>("SELECT forum_name FROM phpbb_forums WHERE forum_id = @forumId", new { ForumId });

                async Task SetTopics()
                    => Topics = await ForumService.GetTopicGroups(ForumId);

                async Task SetReports()
                    => Reports = await _moderatorService.GetReportedMessages(0);

                async Task SetDeletedItems()
                {
                    var allItems = await SqlExecuter.QueryAsync<DeletedItemDto>(
                        @"SELECT rb.id, rb.delete_user, coalesce(u.username, @anonymous) AS delete_user_name, delete_time, content AS raw_content, type
                           FROM phpbb_recycle_bin rb
                           LEFT JOIN phpbb_users ON rb.delete_user = u.user_id
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
                }
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
                    var results = await Task.WhenAll(
                        GetTopicIds().Select(
                            topicId => TopicAction switch
                            {
                                ModeratorTopicActions.MakeTopicNormal => _moderatorService.ChangeTopicType(topicId, TopicType.Normal, logDto),
                                ModeratorTopicActions.MakeTopicImportant => _moderatorService.ChangeTopicType(topicId, TopicType.Important, logDto),
                                ModeratorTopicActions.MakeTopicAnnouncement => _moderatorService.ChangeTopicType(topicId, TopicType.Announcement, logDto),
                                ModeratorTopicActions.MakeTopicGlobal => _moderatorService.ChangeTopicType(topicId, TopicType.Global, logDto),
                                ModeratorTopicActions.MoveTopic => _moderatorService.MoveTopic(topicId, DestinationForumId ?? 0, logDto),
                                ModeratorTopicActions.LockTopic => _moderatorService.LockUnlockTopic(topicId, true, logDto),
                                ModeratorTopicActions.UnlockTopic => _moderatorService.LockUnlockTopic(topicId, false, logDto),
                                ModeratorTopicActions.CreateShortcut => _moderatorService.CreateShortcut(topicId, DestinationForumId ?? 0, logDto),
                                ModeratorTopicActions.RemoveShortcut => _moderatorService.RemoveShortcut(topicId, ForumId, logDto),
                                _ => throw new NotSupportedException($"Can't perform action '{TopicAction}'")
                            }
                        )
                    );

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

            await Task.WhenAll(
                SqlExecuter.ExecuteAsync(
                    @$"INSERT INTO phpbb_forums 
                       VALUES (@ForumId, @ParentId, @LeftId, @RightId, @ForumParents, @ForumName, @ForumDesc, @ForumDescBitfield, @ForumDescOptions, @ForumDescUid, @ForumLink, @ForumPassword, @ForumStyle, @ForumImage, @ForumRules, @ForumRulesLink, @ForumRulesBitfield, @ForumRulesOptions, @ForumRulesUid, @ForumTopicsPerPage, @ForumType, @ForumStatus, @ForumPosts, @ForumTopics, @ForumTopicsReal, @ForumLastPostId, @ForumLastPosterId, @ForumLastPostSubject, @ForumLastPostTime, @ForumLastPosterName, @ForumLastPosterColour, @ForumFlags, @ForumOptions, @DisplaySubforumList, @DisplayOnIndex, @EnableIndexing, @EnableIcons, @EnablePrune, @PruneNext, @PruneDays, @PruneViewed, @PruneFreq, @ForumEditTime);",
                    toAdd),
                SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_recycle_bin WHERE type = @forumType AND id = @forumId",
                    new
                    {
                        forumType = RecycleBinItemType.Forum,
                        ForumId
                    }));

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

            await Task.WhenAll(
                SqlExecuter.ExecuteAsync(
                    @$"INSERT INTO phpbb_topics
                       VALUES (@TopicId, @ForumId, @IconId, @TopicAttachment, @TopicApproved, @TopicReported, @TopicTitle, @TopicPoster, @TopicTime, @TopicTimeLimit, @TopicViews, @TopicReplies, @TopicRepliesReal, @TopicStatus, @TopicType, @TopicFirstPostId, @TopicFirstPosterName, @TopicFirstPosterColour, @TopicLastPostId, @TopicLastPosterId, @TopicLastPosterName, @TopicLastPosterColour, @TopicLastPostSubject, @TopicLastPostTime, @TopicLastViewTime, @TopicMovedId, @TopicBumped, @TopicBumper, @PollTitle, @PollStart, @PollLength, @PollMaxOptions, @PollLastVote, @PollVoteChange);",
                    toAdd),
                SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_recycle_bin WHERE type = @topicType AND id = @topicId",
                    new
                    {
                        topicType = RecycleBinItemType.Topic,
                        topicId
                    }));

            var deletedPosts = await SqlExecuter.QueryAsync<PhpbbRecycleBin>(
                "SELECT * FROM phpbb_recycle_bin WHERE type = @postType",
                new { postType = RecycleBinItemType.Post });               
            var posts = await Task.WhenAll(deletedPosts.Select(dp => CompressionUtility.DecompressObject<PostDto>(dp.Content)));
            foreach (var post in posts.Where(p => p!.TopicId == topicId))
            {
                await RestorePost(post!.PostId);
            }

            await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.RestoreTopic, ForumUser.UserId, toAdd.TopicId);

            return true;

            async Task<bool> ParentForumExists()
            {
                var result = await SqlExecuter.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_forums WHERE forum_id = @forumId",
                    new { forumId = dto?.ForumId ?? 0 });
                return result == 1;
            }
        }

        private async Task<bool> RestorePost(int postId)
        {
            var deletedItem = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbRecycleBin>(
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
            var toAdd = new PhpbbPosts
            {
                PosterId = dto!.AuthorId,
                PostUsername = dto.AuthorName!,
                BbcodeUid = dto.BbcodeUid!,
                ForumId = dto.ForumId,
                TopicId = dto.TopicId,
                PostId = dto.PostId,
                PostTime = dto.PostTime,
                PostSubject = dto.PostSubject!,
                PostText = dto.PostText!
            };

            await SqlExecuter.ExecuteAsync(
                @"INSERT INTO phpbb_posts
                  VALUES (@PostId, @TopicId, @ForumId, @PosterId, @IconId, @PosterIp, @PostTime, @PostApproved, @PostReported, @EnableBbcode, @EnableSmilies, @EnableMagicUrl, @EnableSig, @PostUsername, @PostSubject, @PostText, @PostChecksum, @PostAttachment, @BbcodeBitfield, @BbcodeUid, @PostPostcount, @PostEditTime, @PostEditReason, @PostEditUser, @PostEditCount, @PostEditLocked);",
                toAdd);


            if (dto.Attachments?.Any() ?? false)
            {
                foreach(var a in dto.Attachments.Select(a => new PhpbbAttachments
                {
                    PostMsgId = dto.PostId,
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
                    await SqlExecuter.ExecuteAsync(
                        @"INSERT INTO phpbb_attachments
                          VALUES (@AttachId, @PostMsgId, @TopicId, @InMessage, @PosterId, @IsOrphan, @PhysicalFilename, @RealFilename, @DownloadCount, @AttachComment, @Extension, @Mimetype, @Filesize, @Filetime, @Thumbnail);",
                        a);
                }
            }

            await _postService.CascadePostAdd(toAdd, false);

            await _operationLogService.LogModeratorPostAction(ModeratorPostActions.RestorePosts, ForumUser.UserId, toAdd, $"<a href=\"{ForumLinkUtility.GetRelativeUrlToPost(toAdd.PostId)}\" target=\"_blank\">LINK</a>");

            return true;

            async Task<bool> ParentTopicExists()
            {
                var result = await SqlExecuter.ExecuteScalarAsync<int>(
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
