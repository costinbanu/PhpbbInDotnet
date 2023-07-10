using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain.Extensions;
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
        protected readonly ISqlExecuter SqlExecuter;

        public AuthenticatedPageModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, IConfiguration configuration)
            : base(translationProvider, userService, configuration)
        {
            ForumService = forumService;
            SqlExecuter = sqlExecuter;
        }

        protected async Task<IActionResult> WithRegisteredUser(Func<ForumUserExpanded, Task<IActionResult>> toDo)
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
            var curForum = await SqlExecuter.QuerySingleOrDefaultAsync<PhpbbForums>(
                "SELECT * FROM phpbb_forums WHERE forum_id = @forumId", 
                new { forumId });

            if (!overrideCheck)
            {
                if (curForum == null)
                {
                    return NotFound();
                }

                var restrictedForums = await ForumService.GetRestrictedForumList(ForumUser, true);
                var tree = await ForumService.GetForumTree(ForumUser, false, false);
                var forumPath = new List<int>();
                if (tree.TryGetValue(new ForumTree { ForumId = forumId }, out var currentTreeNode))
                {
                    forumPath = currentTreeNode?.PathList ?? new List<int>();
                }
                
                var restrictedAncestor = (
                    from currentForumId in forumPath

                    join restrictedForum in restrictedForums
                    on currentForumId equals restrictedForum.forumId
                    into joinedForums

                    from joinedForum in joinedForums
                    where !joinedForum.hasPassword || !Request.Cookies.IsUserLoggedIntoForum(ForumUser.UserId, currentForumId)
                    select currentForumId
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
            var curTopic = await SqlExecuter.QuerySingleOrDefaultAsync<PhpbbTopics>(
                "SELECT * FROM phpbb_topics WHERE topic_id = @topicId", 
                new { topicId });

            if (curTopic == null)
            {
                return NotFound();
            }
            return await WithValidForum(curTopic.ForumId, curForum => toDo(curForum, curTopic), forumLoginReturnUrl);
        }

        protected async Task<IActionResult> WithValidPost(int postId, Func<PhpbbForums, PhpbbTopics, PhpbbPosts, Task<IActionResult>> toDo, string? forumLoginReturnUrl = null)
        {
            var curPost = await SqlExecuter.QuerySingleOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE post_id = @postId", 
                new { postId });

            if (curPost == null)
            {
                return NotFound();
            }
            return await WithValidTopic(curPost.TopicId, (curForum, curTopic) => toDo(curForum, curTopic, curPost), forumLoginReturnUrl);
        }

    }
}
