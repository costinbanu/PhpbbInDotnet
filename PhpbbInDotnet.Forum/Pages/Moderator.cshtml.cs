using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ModeratorModel : AuthenticatedPageModel
    {
        private readonly ModeratorService _moderatorService;
        private readonly PostService _postService;

        [BindProperty(SupportsGet = true)]
        public ModeratorPanelMode Mode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty]
        public int[] SelectedReports { get; set; }

        [BindProperty]
        public int[] SelectedTopics { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SelectedTopicIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        [BindProperty]
        public string[] SelectedDeletedItems { get; set; }

        public string ForumName { get; private set; }
        public List<TopicGroup> Topics { get; private set; }
        public List<ReportDto> Reports { get; private set; }
        public string MessageClass { get; private set; }
        public string Message { get; private set; }
        public bool ScrollToAction => TopicAction.HasValue && DestinationForumId.HasValue;
        public IEnumerable<DeletedItemGroup> DeletedItems { get; private set; }

        public ModeratorModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils,
             IConfiguration config, AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider, ModeratorService moderatorService, PostService postService)
            : base(context, forumService, userService, cache, config, sessionCounter, utils, languageProvider)
        {
            _moderatorService = moderatorService;
            _postService = postService;
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
                    => ForumName = (await Context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId))?.ForumName;

                async Task SetTopics()
                {
                    var dbTopics = await (
                        from t in Context.PhpbbTopics.AsNoTracking()
                        where t.ForumId == ForumId
                        orderby t.TopicLastPostTime descending
                        select new TopicDto
                        {
                            TopicId = t.TopicId,
                            TopicTitle = t.TopicTitle,
                            TopicStatus = t.TopicStatus,
                            TopicType = t.TopicType,
                            TopicLastPosterColour = t.TopicLastPosterColour,
                            TopicLastPosterId = t.TopicLastPosterId,
                            TopicLastPosterName = t.TopicLastPosterName,
                            TopicLastPostId = t.TopicLastPostId,
                            TopicLastPostTime = t.TopicLastPostTime
                        }
                    ).ToListAsync();

                    Topics = (
                        from t in dbTopics
                        group t by t.TopicType into groups
                        orderby groups.Key descending
                        select new TopicGroup
                        {
                            TopicType = groups.Key,
                            Topics = groups
                        }
                    ).ToList();
                }

                async Task SetReports()
                    => Reports = await _moderatorService.GetReportedMessages(0);

                async Task SetDeletedItems()
                {
                    var anonymous = LanguageProvider.BasicText[await GetLanguage(), "ANONYMOUS", Casing.None];
                    var allItems = await (
                        from rb in Context.PhpbbRecycleBin.AsNoTracking()

                        join u in Context.PhpbbUsers.AsNoTracking()
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
                            Content = rb.Content,
                            Type = rb.Type
                        }
                    ).ToListAsync();

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

        public async Task<IActionResult> OnPostSubmitReports()
            => await WithModerator(ForumId, async () =>
            {
                var lang = await GetLanguage();
                if (!SelectedReports.Any())
                {
                    MessageClass = "message warning";
                    Message = LanguageProvider.Moderator[lang, "NO_REPORTS_SELECTED"];
                }
                else
                {
                    try
                    {
                        var connection = await Context.GetDbConnectionAsync();
                        await connection.ExecuteAsync(
                            "UPDATE phpbb_reports SET report_closed = 1 WHERE report_id IN @ids",
                            new { ids = SelectedReports ?? new[] { 0 } }
                        );
                        MessageClass = "message success";
                        Message = LanguageProvider.Moderator[lang, "REPORTS_CLOSED_SUCCESSFULLY"];
                    }
                    catch (Exception ex)
                    {
                        var id = Utils.HandleError(ex);
                        MessageClass = "message fail";
                        Message = string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                    }
                }
                return await OnGet();
            });

        public async Task<IActionResult> OnPostSubmitTopics()
            => await WithModerator(ForumId, async () =>
            {
                var lang = await GetLanguage();
                var logDto = new OperationLogDto
                {
                    Action = TopicAction,
                    UserId = (await GetCurrentUserAsync()).UserId
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
                                _ => throw new NotSupportedException($"Can't perform action '{TopicAction}'")
                            }
                        )
                    );

                    if (results.All(r => r.IsSuccess ?? false))
                    {
                        MessageClass = "message success";
                        Message = string.Format(LanguageProvider.Moderator[lang, "MODERATOR_TOPIC_ACTION_SUCCESSFUL_FORMAT"], TopicAction);
                    }
                    else if (results.All(r => !(r.IsSuccess ?? false)))
                    {
                        throw new AggregateException(results.Select(r => new Exception(r.Message)));
                    }
                    else
                    {
                        var failed = results.Where(r => !(r.IsSuccess ?? false));
                        var id = Utils.HandleErrorAsWarning(new AggregateException(failed.Select(r => new Exception(r.Message))));
                        MessageClass = "message warning";
                        Message = string.Format(LanguageProvider.Moderator[lang, "MODERATOR_ACTION_PARTIAL_FAILED_FORMAT"], id);
                    }
                }
                catch (Exception ex)
                {
                    var id = Utils.HandleError(ex);
                    MessageClass = "message fail";
                    Message = string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                }

                TopicAction = null;
                DestinationForumId = null;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostSubmitDeletedItems()
            => await WithModerator(0, async () =>
            {
                var lang = await GetLanguage();
                try
                {
                    var items = SelectedDeletedItems.Select(i =>
                    {
                        var elements = i.Split('/');
                        var type = Enum.Parse<RecycleBinItemType>(elements[0]);
                        var id = int.Parse(elements[1]);
                        return (type, id);
                    }).ToList();

                    if (!await IsCurrentUserAdminHere() && items.Any(item => item.type == RecycleBinItemType.Forum))
                    {
                        MessageClass = "message fail";
                        Message = string.Format(LanguageProvider.Errors[lang, "MISSING_REQUIRED_PERMISSIONS"]);
                    }
                    else
                    {
                        var results = await Task.WhenAll(
                            items.OrderByDescending(i => i.type).Select(
                                i => i.type switch
                                {
                                    RecycleBinItemType.Forum => RestoreForum(i.id),
                                    RecycleBinItemType.Topic => RestoreTopic(i.id),
                                    RecycleBinItemType.Post => RestorePost(i.id),
                                    _ => throw new NotSupportedException($"Can't restore item of type '{i.type}'.")
                                }
                            )
                        );
                        if (results.All(r => r))
                        {
                            MessageClass = "message success";
                            Message = LanguageProvider.Moderator[lang, "ITEMS_RESTORED_SUCCESSFULLY"];
                        }
                        else if (results.All(r => !r))
                        {
                            MessageClass = "message fail";
                            Message = LanguageProvider.Moderator[lang, "ITEMS_RESTORATION_FAILED"];
                        }
                        else
                        {
                            MessageClass = "message fail";
                            Message = LanguageProvider.Moderator[lang, "ITEMS_RESTORATION_FAILED_PARTIALLY"];
                        }
                    }
                }
                catch (Exception ex)
                {
                    var id = Utils.HandleError(ex);
                    MessageClass = "message fail";
                    Message = string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                }

                TopicAction = null;
                DestinationForumId = null;
                return await OnGet();

                async Task<bool> RestoreForum(int forumId)
                {
                    var deletedItem = await Context.PhpbbRecycleBin.FirstOrDefaultAsync(r => r.Type == RecycleBinItemType.Forum && r.Id == forumId);
                    if (deletedItem == null)
                    {
                        return false;
                    }
                    var dto = await Utils.DecompressObject<ForumDto>(deletedItem.Content);
                    Context.PhpbbForums.Add(new PhpbbForums
                    {
                        ForumId = dto.ForumId.Value,
                        ForumName = dto.ForumName,
                        ForumDesc = dto.ForumDesc,
                        ForumPassword = dto.ForumPassword,
                        ParentId = dto.ParentId.Value,
                        ForumType = dto.ForumType.Value,
                        ForumRules = dto.ForumRules,
                        ForumRulesLink = dto.ForumRulesLink,
                        ForumLastPostId = dto.ForumLastPostId,
                        ForumLastPosterId = dto.ForumLastPosterId,
                        ForumLastPostSubject = dto.ForumLastPostSubject,
                        ForumLastPostTime = dto.ForumLastPostTime,
                        ForumLastPosterName = dto.ForumLastPosterName,
                        ForumLastPosterColour = dto.ForumLastPosterColour
                    });
                    Context.PhpbbRecycleBin.Remove(deletedItem);
                    await Context.SaveChangesAsync();
                    return true;
                }

                async Task<bool> RestoreTopic(int topicId)
                {
                    var deletedItem = await Context.PhpbbRecycleBin.FirstOrDefaultAsync(r => r.Type == RecycleBinItemType.Topic && r.Id == topicId);
                    if (deletedItem == null)
                    {
                        return false;
                    }
                    var dto = await Utils.DecompressObject<TopicDto>(deletedItem.Content);
                    if (!Context.PhpbbForums.AsNoTracking().Any(f => f.ForumId == dto.ForumId))
                    {
                        if (!await RestoreForum(dto.ForumId.Value))
                        {
                            return false;
                        }
                        if (!Context.PhpbbForums.AsNoTracking().Any(f => f.ForumId == dto.ForumId))
                        {
                            return false;
                        }
                    }
                    var toAdd = new PhpbbTopics
                    {
                        ForumId = dto.ForumId.Value,
                        TopicId = dto.TopicId.Value,
                        TopicTitle = dto.TopicTitle,
                        TopicStatus = dto.TopicStatus,
                        TopicType = dto.TopicType.Value,
                        TopicLastPosterColour = dto.TopicLastPosterColour,
                        TopicLastPosterId = dto.TopicLastPosterId.Value,
                        TopicLastPosterName = dto.TopicLastPosterName,
                        TopicLastPostTime = dto.TopicLastPostTime.Value
                    };
                    if (dto.Poll != null)
                    {
                        toAdd.PollTitle = dto.Poll.PollTitle;
                        toAdd.PollStart = dto.Poll.PollStart.ToUnixTimestamp();
                        toAdd.PollLength = dto.Poll.PollDurationSecons;
                        toAdd.PollMaxOptions = (byte)dto.Poll.PollMaxOptions;
                        toAdd.PollVoteChange = dto.Poll.VoteCanBeChanged.ToByte();
                        Context.PhpbbPollOptions.AddRange(dto.Poll.PollOptions.Select(opt => new PhpbbPollOptions
                        {
                            PollOptionId = opt.PollOptionId,
                            PollOptionText = opt.PollOptionText,
                            TopicId = opt.TopicId
                        }));
                    }
                    Context.PhpbbTopics.Add(toAdd);
                    Context.PhpbbRecycleBin.Remove(deletedItem);
                    await Context.SaveChangesAsync();
                    return true;
                }

                async Task<bool> RestorePost(int postId)
                {
                    var deletedItem = await Context.PhpbbRecycleBin.FirstOrDefaultAsync(r => r.Type == RecycleBinItemType.Post && r.Id == postId);
                    if (deletedItem == null)
                    {
                        return false;
                    }
                    var dto = await Utils.DecompressObject<PostDto>(deletedItem.Content);
                    if (!Context.PhpbbTopics.AsNoTracking().Any(t => t.TopicId == dto.TopicId))
                    {
                        if (!await RestoreTopic(dto.TopicId))
                        {
                            return false;
                        }

                        if (!Context.PhpbbTopics.AsNoTracking().Any(t => t.TopicId == dto.TopicId))
                        {
                            return false;
                        }
                    }
                    var toAdd = new PhpbbPosts
                    {
                        PosterId = dto.AuthorId,
                        PostUsername = dto.AuthorName,
                        BbcodeUid = dto.BbcodeUid,
                        ForumId = dto.ForumId,
                        TopicId = dto.TopicId,
                        PostId = dto.PostId,
                        PostTime = dto.PostTime,
                        PostSubject = dto.PostSubject,
                        PostText = dto.PostText
                    };
                    Context.PhpbbPosts.Add(toAdd);
                    if (dto.Attachments?.Any() ?? false)
                    {
                        Context.PhpbbAttachments.AddRange(dto.Attachments.Select(a => new PhpbbAttachments
                        {
                            PostMsgId = dto.PostId,
                            PosterId = dto.AuthorId,
                            RealFilename = a.DisplayName,
                            AttachComment = a.Comment,
                            AttachId = a.Id,
                            Mimetype = a.MimeType,
                            DownloadCount = a.DownloadCount,
                            Filesize = a.FileSize,
                            PhysicalFilename = a.PhysicalFileName
                        }));
                    }
                    Context.PhpbbRecycleBin.Remove(deletedItem);
                    await Context.SaveChangesAsync();
                    await _postService.CascadePostAdd(toAdd, false, true);
                    return true;
                }
            });
    }
}
