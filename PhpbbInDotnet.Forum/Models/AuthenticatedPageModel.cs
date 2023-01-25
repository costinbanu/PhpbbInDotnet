using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
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
        protected readonly IUserService UserService;
        protected readonly ISqlExecuter SqlExecuter;

        public AuthenticatedPageModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider)
            : base(translationProvider)
        {
            ForumService = forumService;
            UserService = userService;
            SqlExecuter = sqlExecuter;
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
                    where !j.hasPassword /*|| Cache.Get<int>(CacheUtility.GetForumLoginCacheKey(ForumUser.UserId, t)) != 1*/
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
