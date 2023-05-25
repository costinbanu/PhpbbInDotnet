using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
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
        private readonly IForumDbContext _dbContext;
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
            ITranslationProvider translationProvider, IForumDbContext dbContext, IModeratorService moderatorService, IPostService postService, 
            IOperationLogService operationLogService, ILogger logger, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _moderatorService = moderatorService; 
            _postService = postService; 
            _operationLogService = operationLogService;
            _dbContext = dbContext;
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
                    => ForumName = (await _dbContext.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId))?.ForumName;

                async Task SetTopics()
                    => Topics = await ForumService.GetTopicGroups(ForumId);

                async Task SetReports()
                    => Reports = await _moderatorService.GetReportedMessages(0);

                async Task SetDeletedItems()
                {
                    var anonymous = TranslationProvider.BasicText[Language, "ANONYMOUS", Casing.None];
                    var allItems = await (
                        from rb in _dbContext.PhpbbRecycleBin.AsNoTracking()

                        join u in _dbContext.PhpbbUsers.AsNoTracking()
                        on rb.DeleteUser equals u.UserId
                        into joinedUsers

                        from ju in joinedUsers.DefaultIfEmpty()

                        orderby rb.DeleteTime descending
                        select new DeletedItemDto
                        {
                            Id = rb.Id,
                            DeleteUser = rb.DeleteUser,
                            DeleteUserName = ju == null ? anonymous : ju.Username,
                            DeleteTime = rb.DeleteTime,
                            RawContent = rb.Content,
                            Type = rb.Type
                        }
                    ).ToListAsync();

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
            var deletedItem = await _dbContext.PhpbbRecycleBin.FirstOrDefaultAsync(r => r.Type == RecycleBinItemType.Forum && r.Id == forumId);
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
            _dbContext.PhpbbForums.Add(toAdd);
            _dbContext.PhpbbRecycleBin.Remove(deletedItem);
            await _dbContext.SaveChangesAsync();

            await _operationLogService.LogAdminForumAction(AdminForumActions.Restore, ForumUser.UserId, toAdd);

            return true;
        }

        private async Task<bool> RestoreTopic(int topicId)
        {
            var deletedItem = await _dbContext.PhpbbRecycleBin.FirstOrDefaultAsync(r => r.Type == RecycleBinItemType.Topic && r.Id == topicId);
            if (deletedItem == null)
            {
                return false;
            }
            var dto = await CompressionUtility.DecompressObject<TopicDto>(deletedItem.Content);
            if (!_dbContext.PhpbbForums.AsNoTracking().Any(f => f.ForumId == dto!.ForumId))
            {
                if (!await RestoreForum(dto!.ForumId!.Value))
                {
                    return false;
                }
                if (!_dbContext.PhpbbForums.AsNoTracking().Any(f => f.ForumId == dto.ForumId))
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
            _dbContext.PhpbbTopics.Add(toAdd);
            _dbContext.PhpbbRecycleBin.Remove(deletedItem);
            await _dbContext.SaveChangesAsync();

            var deletedPosts = await _dbContext.PhpbbRecycleBin.Where(rb => rb.Type == RecycleBinItemType.Post).ToListAsync();
            var posts = await Task.WhenAll(deletedPosts.Select(dp => CompressionUtility.DecompressObject<PostDto>(dp.Content)));
            foreach (var post in posts.Where(p => p!.TopicId == topicId))
            {
                await RestorePost(post!.PostId);
            }

            await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.RestoreTopic, ForumUser.UserId, toAdd.TopicId);

            return true;
        }

        private async Task<bool> RestorePost(int postId)
        {
            var deletedItem = await _dbContext.PhpbbRecycleBin.FirstOrDefaultAsync(r => r.Type == RecycleBinItemType.Post && r.Id == postId);
            if (deletedItem == null)
            {
                return false;
            }
            var dto = await CompressionUtility.DecompressObject<PostDto>(deletedItem.Content);
            if (!_dbContext.PhpbbTopics.AsNoTracking().Any(t => t.TopicId == dto!.TopicId))
            {
                if (!await RestoreTopic(dto!.TopicId))
                {
                    return false;
                }

                if (!_dbContext.PhpbbTopics.AsNoTracking().Any(t => t.TopicId == dto.TopicId))
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
            _dbContext.PhpbbPosts.Add(toAdd);
            if (dto.Attachments?.Any() ?? false)
            {
                _dbContext.PhpbbAttachments.AddRange(dto.Attachments.Select(a => new PhpbbAttachments
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
                }));
            }
            _dbContext.PhpbbRecycleBin.Remove(deletedItem);
            await _dbContext.SaveChangesAsync();
            await _postService.CascadePostAdd(toAdd, false);

            await _operationLogService.LogModeratorPostAction(ModeratorPostActions.RestorePosts, ForumUser.UserId, toAdd, $"<a href=\"{ForumLinkUtility.GetRelativeUrlToPost(toAdd.PostId)}\" target=\"_blank\">LINK</a>");

            return true;
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
