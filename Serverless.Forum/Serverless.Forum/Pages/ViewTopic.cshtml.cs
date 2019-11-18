using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ViewTopicModel : ModelWithLoggedUser
    {
        public List<PostDisplay> Posts { get; private set; }
        public string TopicTitle { get; private set; }
        public string ForumTitle { get; private set; }
        public int? ForumId { get; private set; }
        public _PaginationPartialModel Pagination { get; private set; }
        public int? PostId { get; private set; }
        public bool IsFirstPage { get; private set; }
        public bool IsLastPage { get; private set; }
        public int CurrentPage { get; private set; }
        public readonly List<SelectListItem> PostsPerPage;
        public bool? Highlight { get; private set; }
        public int? TopicId => _currentTopic?.TopicId;
        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;
        public IConfiguration Config => _config;
        public Utils Utils => _utils;

        private PhpbbTopics _currentTopic;
        private List<PhpbbPosts> _dbPosts;
        private int? _page;
        private int? _count;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpContext _context;
        private readonly ITempDataProvider _tempDataProvider;

        public ViewTopicModel(IConfiguration config, Utils utils, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider, IHttpContextAccessor accessor) : base(config, utils)
        {
            _viewEngine = viewEngine;
            PostsPerPage = new List<SelectListItem>
            {
                new SelectListItem("7", "7", false),
                new SelectListItem("14", "14", false),
                new SelectListItem("28", "28", false),
                new SelectListItem("56", "56", false),
                new SelectListItem("112", "112", false)
            };

            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            _context = accessor?.HttpContext;
        }

        public async Task<IActionResult> OnGetByPostId(int postId, bool? highlight)
        {
            if (_currentTopic == null)
            {
                using (var context = new forumContext(_config))
                {
                    _currentTopic = await (
                        from p in context.PhpbbPosts
                        where p.PostId == postId

                        join t in context.PhpbbTopics
                        on p.TopicId equals t.TopicId
                        into joined

                        from j in joined
                        select j
                    ).FirstOrDefaultAsync();
                }
            }

            if (_currentTopic == null)
            {
                return NotFound($"Mesajul {postId} nu există.");
            }

            await GetPostsLazy(null, null, postId);

            PostId = postId;
            Highlight = highlight;
            return await OnGet(_currentTopic.TopicId, _page.Value);
        }

        public async Task<IActionResult> OnGet(int topicId, int pageNum)
        {
            PhpbbForums parent = null;
            using (var context = new forumContext(_config))
            {
                if (_currentTopic == null)
                {
                    _currentTopic = await (from t in context.PhpbbTopics
                                           where t.TopicId == topicId
                                           select t).FirstOrDefaultAsync();
                }

                if (_currentTopic == null)
                {
                    return NotFound($"Subiectul {topicId} nu există.");
                }

                parent = await (from f in context.PhpbbForums

                                join t in context.PhpbbTopics
                                on f.ForumId equals t.ForumId
                                into joined

                                from j in joined
                                where j.TopicId == topicId
                                select f).FirstOrDefaultAsync();
            }

            var usr = await GetCurrentUserAsync();
            var pageSize = usr.TopicPostsPerPage.ContainsKey(topicId) ? usr.TopicPostsPerPage[topicId] : 14;
            PostsPerPage.ForEach(ppp => ppp.Selected = int.TryParse(ppp.Value, out var value) && value == pageSize);
            ForumId = parent?.ForumId;

            if (!string.IsNullOrEmpty(parent.ForumPassword) &&
                (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != ForumId)
            {
                if ((await GetCurrentUserAsync()).UserPermissions.Any(fp => fp.ForumId == ForumId && fp.AuthRoleId == 16))
                {
                    return Unauthorized();
                }
                else
                {
                    return RedirectToPage("ForumLogin", new ForumLoginModel(_config)
                    {
                        ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                        ForumId = parent.ForumId,
                        ForumName = parent.ForumName
                    });
                }
            }

            ForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? "untitled");

            await GetPostsLazy(topicId, pageNum, null);
            var noOfPages = (_count.Value / pageSize) + (_count.Value % pageSize == 0 ? 0 : 1);
            if (pageNum > noOfPages)
            {
                pageNum = noOfPages;
            }
            if (pageNum < 1)
            {
                pageNum = 1;
            }

            IsFirstPage = pageNum == 1;
            IsLastPage = pageNum == noOfPages;
            CurrentPage = pageNum;

            using (var context = new forumContext(_config))
            {
                //var postsInPage = (
                Posts = (
                    from p in _dbPosts

                    join u in context.PhpbbUsers
                    on p.PosterId equals u.UserId
                    into joinedUsers

                    join a in context.PhpbbAttachments
                    on p.PostId equals a.PostMsgId
                    into joinedAttachments

                    from ju in joinedUsers.DefaultIfEmpty()

                    select new PostDisplay
                    {
                        PostSubject = p.PostSubject,
                        PostText = p.PostText,
                        AuthorName = ju == null ? "Anonymous" : (ju.UserId == 1 ? p.PostUsername : ju.Username),
                        AuthorId = ju == null ? 1 : (ju.UserId == 1 ? null as int? : ju.UserId),
                        AuthorColor = ju == null ? null : ju.UserColour,
                        PostCreationTime = p.PostTime.TimestampToLocalTime(),
                        PostModifiedTime = p.PostEditTime.TimestampToLocalTime(),
                        PostId = p.PostId,
                        Attachments = (from ja in joinedAttachments
                                       select ja.ToModel()).ToList(),
                        BbcodeUid = p.BbcodeUid,

                        Unread = IsPostUnread(p.TopicId, p.PostId)
                    }
                ).ToList();

                var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, _AttachmentPartialModel Attach)>();


                Parallel.ForEach(/*postsInPage*/Posts, (p, state1) =>
                {
                    p.PostSubject = HttpUtility.HtmlDecode(p.PostSubject);
                    p.PostText = HttpUtility.HtmlDecode(_utils.BBCodeParser.ToHtml(p.PostText, p.BbcodeUid));
                    p.PostText = _utils.NewLineRegex.Replace(p.PostText, "<br/>");
                    p.PostText = _utils.HtmlCommentRegex.Replace(p.PostText, string.Empty);
                    p.PostText = _utils.SmileyRegex.Replace(p.PostText, Constants.SMILEY_PATH);

                    Parallel.ForEach(p.Attachments, (candidate, state2) =>
                    {
                        if (p.PostText.Contains($"##AttachmentFileName={candidate.FileName}##"))
                        {
                            inlineAttachmentsPosts.Add((p.PostId.Value, candidate));
                        }
                    });
                });
                // Posts = postsInPage.ToList();
                Posts.ForEach(async (p) =>
                {
                    foreach (var candidate in from a in inlineAttachmentsPosts
                                              where a.PostId == p.PostId
                                              select a)
                    {
                        p.PostText = p.PostText.Replace(
                            $"##AttachmentFileName={candidate.Attach.FileName}##",
                            await RenderRazorViewToString(
                                "_AttachmentPartial",
                                candidate.Attach
                            )
                        );
                        p.Attachments.Remove(candidate.Attach);
                    }
                });
                TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");

                Pagination = new _PaginationPartialModel(
                    $"/ViewTopic?TopicId={topicId}&PageNum=1",
                    _count.Value,
                    pageSize,
                    pageNum
                );
            }

            return Page();
        }

        public async Task<IActionResult> OnPost(int topicId, int userPostsPerPage, int postId)
        {
            async Task save(forumContext localContext)
            {
                await localContext.SaveChangesAsync();
                await ReloadCurrentUser();
            }

            using (var context = new forumContext(_config))
            {
                var curValue = await context.PhpbbUserTopicPostNumber
                                               .FirstOrDefaultAsync(ppp => ppp.UserId == CurrentUserId &&
                                                                           ppp.TopicId == topicId);

                if (curValue == null)
                {
                    context.PhpbbUserTopicPostNumber.Add(
                        new PhpbbUserTopicPostNumber
                        {
                            UserId = CurrentUserId.Value,
                            TopicId = topicId,
                            PostNo = userPostsPerPage
                        }
                    );
                    await save(context);
                }
                else if (curValue.PostNo != userPostsPerPage)
                {
                    curValue.PostNo = userPostsPerPage;
                    await save(context);
                }
                return RedirectToPage("ViewTopic", "ByPostId", new { postId = postId, highlight = false });
            }
        }

        private async Task<string> RenderRazorViewToString(string viewName, _AttachmentPartialModel model)
        {
            try
            {
                var actionContext = new ActionContext(_context, _context.GetRouteData(), PageContext.ActionDescriptor);


                var viewResult = _viewEngine.FindView(actionContext, viewName, false); 

                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"{viewName} does not match any available view");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = model
                };
                using (var sw = new StringWriter())
                {
                    var viewContext = new ViewContext(
                        actionContext,
                        viewResult.View,
                        viewDictionary,
                        new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                        sw,
                        new HtmlHelperOptions()
                    )
                    {
                        RouteData = _context.GetRouteData()
                    };

                    await viewResult.View.RenderAsync(viewContext);
                    return sw.GetStringBuilder().ToString();
                }
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        private async Task GetPostsLazy(int? topicId, int? page, int? postId)
        {
            if (_dbPosts == null || _page == null || _count == null)
            {
                var results = await _utils.GetPostPageAsync(CurrentUserId.Value, topicId, page, postId);
                _dbPosts = results.Posts;
                _page = results.Page;
                _count = results.Count;
            }
        }
    }
}