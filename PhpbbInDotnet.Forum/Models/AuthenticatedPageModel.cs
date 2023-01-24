using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Models
{
    public abstract class AuthenticatedPageModel : BaseModel
    {
        protected readonly IForumTreeService ForumService;
        protected readonly IAppCache Cache;
        protected readonly IUserService UserService;
        protected readonly IForumDbContext Context;
        protected readonly ISqlExecuter SqlExecuter;
        protected readonly ILogger Logger;

        public ITranslationProvider TranslationProvider { get; }

        private string? _language;

        public AuthenticatedPageModel(IServiceProvider serviceProvider)
        {
            ForumService = serviceProvider.GetRequiredService<IForumTreeService>();
            Cache = serviceProvider.GetRequiredService<IAppCache>();
            UserService = serviceProvider.GetRequiredService<IUserService>();
            Context = serviceProvider.GetRequiredService<IForumDbContext>();
            SqlExecuter = serviceProvider.GetRequiredService<ISqlExecuter>();
            Logger = serviceProvider.GetRequiredService<ILogger>();
            TranslationProvider = serviceProvider.GetRequiredService<ITranslationProvider>();
        }

        public AuthenticatedUserExpanded ForumUser
            => AuthenticatedUserExpanded.GetValue(HttpContext);

        public string Language
            => _language ??= TranslationProvider.GetLanguage(ForumUser);

        public async Task<(HashSet<ForumTree> Tree, Dictionary<int, HashSet<Tracking>>? Tracking)> GetForumTree(bool forceRefresh, bool fetchUnreadData)
            => (Tree: await ForumService.GetForumTree(ForumUser, forceRefresh, fetchUnreadData),
                Tracking: fetchUnreadData ? await ForumService.GetForumTracking(ForumUser.UserId, forceRefresh) : null);

        protected async Task MarkForumAndSubforumsRead(int forumId)
        {
            var node = ForumService.GetTreeNode((await GetForumTree(false, false)).Tree, forumId);
            if (node == null)
            {
                if (forumId == 0)
                {
                    await SetLastMark();
                }
                return;
            }

            await MarkForumRead(forumId);
            foreach (var child in node.ChildrenList ?? new HashSet<int>())
            {
                await MarkForumAndSubforumsRead(child);
            }
        }

        protected async Task MarkForumRead(int forumId)
        {
            try
            {
                var usrId = ForumUser.UserId;
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_topics_track WHERE forum_id = @forumId AND user_id = @usrId; " +
                    "REPLACE INTO phpbb_forums_track (forum_id, user_id, mark_time) VALUES (@forumId, @usrId, @markTime);",
                    new { forumId, usrId, markTime = DateTime.UtcNow.ToUnixTimestamp() }
                );
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error marking forums as read.");
            }
        }

        public async Task MarkTopicRead(int forumId, int topicId, bool isLastPage, long markTime)
        {
            var (_, tracking) = await GetForumTree(false, true);
            var userId = ForumUser.UserId;
            if (tracking!.TryGetValue(forumId, out var tt) && tt.Count == 1 && isLastPage)
            {
                //current topic was the last unread in its forum, and it is the last page of unread messages, so mark the whole forum read
                await MarkForumRead(forumId);

                //current forum is the user's last unread forum, and it has just been read; set the mark time.
                if (tracking.Count == 1)
                {
                    await SetLastMark();
                }
            }
            else
            {
                //there are other unread topics in this forum, or unread pages in this topic, so just mark the current page as read
                try
                {
                    await SqlExecuter.ExecuteAsync(
                        sql: "CALL mark_topic_read(@forumId, @topicId, @userId, @markTime)",
                        param: new { forumId, topicId, userId, markTime }
                    );
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Error marking topics as read (forumId={forumId}, topicId={topicId}, userId={userId}).", forumId, topicId, userId);
                }
            }
        }

        private async Task SetLastMark()
        {
            var usrId = ForumUser.UserId;
            try
            {
                var sqlExecuter = SqlExecuter;
                await sqlExecuter.ExecuteAsync("UPDATE phpbb_users SET user_lastmark = @markTime WHERE user_id = @usrId", new { markTime = DateTime.UtcNow.ToUnixTimestamp(), usrId });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error setting user last mark.");
            }
        }

        protected async Task<IEnumerable<int>> GetRestrictedForums()
        {
            var restrictedForums = await ForumService.GetRestrictedForumList(ForumUser);
            return restrictedForums.Select(f => f.forumId).DefaultIfEmpty();
        }

        protected async Task<IActionResult> WithRegisteredUser(Func<AuthenticatedUserExpanded, Task<IActionResult>> toDo)
        {
            if (ForumUser.IsAnonymous)
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo(ForumUser);
        }

        protected async Task<IActionResult> WithModerator(int forumId, Func<Task<IActionResult>> toDo)
        {
            if (!await UserService.IsUserModeratorInForum(ForumUser, forumId))
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithAdmin(Func<Task<IActionResult>> toDo)
        {
            if (!await UserService.IsAdmin(ForumUser))
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithValidForum(int forumId, bool overrideCheck, Func<PhpbbForums, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
        {
            var sqlExecuter = SqlExecuter;
            var curForum = await sqlExecuter.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId });

            if (!overrideCheck)
            {
                if (curForum == null)
                {
                    return NotFound();
                }

                var restricted = await ForumService.GetRestrictedForumList(ForumUser, true);
                var tree = await ForumService.GetForumTree(ForumUser, false, false);
                var path = new List<int>();
                if (tree.TryGetValue(new ForumTree { ForumId = forumId }, out var cur))
                {
                    path = cur?.PathList ?? new List<int>();
                }

                var restrictedAncestor = (
                    from t in path
                    join r in restricted
                    on t equals r.forumId
                    into joined
                    from j in joined
                    where !j.hasPassword || Cache.Get<int>(CacheUtility.GetForumLoginCacheKey(ForumUser.UserId, t)) != 1
                    select t
                ).FirstOrDefault();

                if (restrictedAncestor != default)
                {
                    if (ForumUser.IsForumRestricted(restrictedAncestor))
                    {
                        return Unauthorized();
                    }
                    else
                    {
                        return RedirectToPage("ForumLogin", new
                        {
                            ReturnUrl = string.IsNullOrWhiteSpace(forumLoginReturnUrl) ? Request.GetEncodedPathAndQuery() : forumLoginReturnUrl,
                            ForumId = restrictedAncestor
                        });
                    }
                }
            }
            return await toDo(curForum);
        }

        protected Task<IActionResult> WithValidForum(int forumId, Func<PhpbbForums, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
            => WithValidForum(forumId, false, toDo, forumLoginReturnUrl);

        protected async Task<IActionResult> WithValidTopic(int topicId, Func<PhpbbForums, PhpbbTopics, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
        {
            var sqlExecuter = SqlExecuter;

            var curTopic = await sqlExecuter.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });

            if (curTopic == null)
            {
                return NotFound();
            }
            return await WithValidForum(curTopic.ForumId, curForum => toDo(curForum, curTopic), forumLoginReturnUrl);
        }

        protected async Task<IActionResult> WithValidPost(int postId, Func<PhpbbForums, PhpbbTopics, PhpbbPosts, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
        {
            var sqlExecuter = SqlExecuter;

            var curPost = await sqlExecuter.QuerySingleOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
            if (curPost == null)
            {
                return NotFound();
            }
            return await WithValidTopic(curPost.TopicId, (curForum, curTopic) => toDo(curForum, curTopic, curPost), forumLoginReturnUrl);
        }

    }
}
