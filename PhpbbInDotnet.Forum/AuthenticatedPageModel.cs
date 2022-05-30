using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum
{
    public class AuthenticatedPageModel : PageModel
    {
        protected readonly IForumTreeService ForumService;
        protected readonly IAppCache Cache;
        protected readonly IUserService IUserService;
        protected readonly IForumDbContext Context;
        protected readonly ICommonUtils Utils;
        
        public LanguageProvider LanguageProvider { get; }

        private string? _language;

        public AuthenticatedPageModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cacheService, ICommonUtils utils, LanguageProvider languageProvider)
        {
            ForumService = forumService;
            Cache = cacheService;
            IUserService = userService;
            Context = context;
            Utils = utils;
            LanguageProvider = languageProvider;
        }

        #region User

        public AuthenticatedUserExpanded GetCurrentUser()
            => (AuthenticatedUserExpanded)HttpContext.Items[nameof(AuthenticatedUserExpanded)]!;

        public void ReloadCurrentUser()
        {
            var user = GetCurrentUser();
            Cache.Add($"ReloadUser_{user.UsernameClean}", true);
        }

        public async Task<bool> IsCurrentUserAdminHere(int forumId = 0)
            => await IUserService.IsUserAdminInForum(GetCurrentUser(), forumId);

        public async Task<bool> IsCurrentUserModeratorHere(int forumId = 0)
            => await IUserService.IsUserModeratorInForum(GetCurrentUser(), forumId);

        public string GetLanguage()
            => _language ??= LanguageProvider.GetValidatedLanguage(GetCurrentUser(), Request);

        public async Task<List<int>> GetUnrestrictedForums(int? forumId = null)
        {
            var (tree, _) = await GetForumTree(false, false);
            var toReturn = new List<int>(tree.Count);

            if ((forumId ?? 0) > 0)
            {
                traverse(forumId!.Value);
            }
            else
            {
                toReturn.AddRange(tree.Where(t => !ForumService.IsNodeRestricted(t)).Select(t => t.ForumId));
            }

            return toReturn;

            void traverse(int fid)
            {
                var node = ForumService.GetTreeNode(tree, fid);
                if (node != null)
                {
                    if (!ForumService.IsNodeRestricted(node))
                    {
                        toReturn.Add(fid);
                    }
                    foreach (var child in node?.ChildrenList ?? new HashSet<int>())
                    {
                        traverse(child);
                    }
                }
            }
        }

        #endregion User

        #region Forum for user

        public async Task<bool> IsForumUnread(int forumId, bool forceRefresh = false)
        {
            var usr = GetCurrentUser();
            return !usr.IsAnonymous && await ForumService.IsForumUnread(forumId, usr, forceRefresh);
        }

        public async Task<bool> IsTopicUnread(int forumId, int topicId, bool forceRefresh = false)
        {
            var usr = GetCurrentUser();
            return !usr.IsAnonymous && await ForumService.IsTopicUnread(forumId, topicId, usr, forceRefresh);
        }

        public async Task<bool> IsPostUnread(int forumId, int topicId, int postId)
        {
            var usr = GetCurrentUser();
            return !usr.IsAnonymous && await ForumService.IsPostUnread(forumId, topicId, postId, usr);
        }

        public async Task<int> GetFirstUnreadPost(int forumId, int topicId)
        {
            if (GetCurrentUser().IsAnonymous)
            {
                return 0;
            }
            Tracking? item = null;
            var found = (await GetForumTree(false, true)).Tracking!.TryGetValue(forumId, out var tt) && tt.TryGetValue(new Tracking { TopicId = topicId }, out item);
            if (!found)
            {
                return 0;
            }

            var sqlExecuter = Context.GetSqlExecuter();
            return unchecked((int)((await sqlExecuter.QuerySingleOrDefaultAsync(
                "SELECT post_id, post_time FROM phpbb_posts WHERE post_id IN @postIds HAVING post_time = MIN(post_time)",
                new { postIds = item!.Posts?.DefaultIfEmpty() ?? new int[] { default } }
            ))?.post_id ?? 0u));
        }

        public async Task<(HashSet<ForumTree> Tree, Dictionary<int, HashSet<Tracking>>? Tracking)> GetForumTree(bool forceRefresh, bool fetchUnreadData)
            => (Tree: await ForumService.GetForumTree(GetCurrentUser(), forceRefresh, fetchUnreadData), 
                Tracking: fetchUnreadData ? await ForumService.GetForumTracking(GetCurrentUser().UserId, forceRefresh) : null);

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
                var usrId = GetCurrentUser().UserId;
                var sqlExecuter = Context.GetSqlExecuter();

                await sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_topics_track WHERE forum_id = @forumId AND user_id = @usrId; " +
                    "REPLACE INTO phpbb_forums_track (forum_id, user_id, mark_time) VALUES (@forumId, @usrId, @markTime);",
                    new { forumId, usrId, markTime = DateTime.UtcNow.ToUnixTimestamp() }
                );
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex, "Error marking forums as read.");
            }
        }

        public async Task MarkTopicRead(int forumId, int topicId, bool isLastPage, long markTime)
        {
            var (_, tracking) = await GetForumTree(false, true);
            var userId = GetCurrentUser().UserId;
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
                    await (Context.GetSqlExecuter()).ExecuteAsync(
                        sql: "CALL mark_topic_read(@forumId, @topicId, @userId, @markTime)",
                        param: new { forumId, topicId, userId, markTime }
                    );
                }
                catch (Exception ex)
                {
                    Utils.HandleErrorAsWarning(ex, $"Error marking topics as read (forumId={forumId}, topicId={topicId}, userId={userId}).");
                }
            }
        }

        protected async Task SetLastMark()
        {
            var usrId = GetCurrentUser().UserId;
            try
            {
                var sqlExecuter = Context.GetSqlExecuter();
                await sqlExecuter.ExecuteAsync("UPDATE phpbb_users SET user_lastmark = @markTime WHERE user_id = @usrId", new { markTime = DateTime.UtcNow.ToUnixTimestamp(), usrId });
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex, "Error setting user last mark.");
            }
        }

        protected async Task<IEnumerable<int>> GetRestrictedForums()
        {
            var restrictedForums = await ForumService.GetRestrictedForumList(GetCurrentUser());
            return restrictedForums.Select(f => f.forumId).DefaultIfEmpty();
        }

        #endregion Forum for user

        #region Permission validation wrappers

        protected async Task<IActionResult> WithRegisteredUser(Func<AuthenticatedUserExpanded, Task<IActionResult>> toDo)
        {
            var user = GetCurrentUser();
            if (user.IsAnonymous)
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo(user);
        }

        protected async Task<IActionResult> WithModerator(int forumId, Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserModeratorHere(forumId))
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithAdmin(Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserAdminHere())
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithValidForum(int forumId, bool overrideCheck, Func<PhpbbForums, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
        {
            var sqlExecuter = Context.GetSqlExecuter();
            var curForum = await sqlExecuter.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId });

            if (!overrideCheck)
            {
                if (curForum == null)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }

                var usr = GetCurrentUser();
                var restricted = await ForumService.GetRestrictedForumList(usr, true);
                var tree = await ForumService.GetForumTree(usr, false, false);
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
                    where !j.hasPassword || Cache.Get<int>(Utils.GetForumLoginCacheKey(usr?.UserId ?? Constants.ANONYMOUS_USER_ID, t)) != 1
                    select t
                ).FirstOrDefault();

                if (restrictedAncestor != default)
                {
                    if (usr.IsForumRestricted(restrictedAncestor))
                    {
                        return RedirectToPage("Error", new { IsUnauthorized = true });
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
            var sqlExecuter = Context.GetSqlExecuter();
            
            var curTopic = await sqlExecuter.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
            
            if (curTopic == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            return await WithValidForum(curTopic.ForumId, curForum => toDo(curForum, curTopic), forumLoginReturnUrl);
        }

        protected async Task<IActionResult> WithValidPost(int postId, Func<PhpbbForums, PhpbbTopics, PhpbbPosts, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
        {
            var sqlExecuter = Context.GetSqlExecuter();

            var curPost = await sqlExecuter.QuerySingleOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
            if (curPost == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            return await WithValidTopic(curPost.TopicId, (curForum, curTopic) => toDo(curForum, curTopic, curPost), forumLoginReturnUrl);
        }

        #endregion Permission validation wrappers
    }
}
