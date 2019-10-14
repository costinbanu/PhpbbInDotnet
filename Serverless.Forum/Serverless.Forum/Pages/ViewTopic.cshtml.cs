using CodeKicker.BBCode.Core;
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
using System.Text.RegularExpressions;
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

        private PhpbbTopics _currentTopic;
        private List<PhpbbPosts> _dbPosts;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpContext _context;
        private readonly ITempDataProvider _tempDataProvider;

        public ViewTopicModel(IConfiguration config, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider, IHttpContextAccessor accessor) : base(config)
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
                    _currentTopic = await (from p in context.PhpbbPosts
                                           where p.PostId == postId

                                           join t in context.PhpbbTopics
                                           on p.TopicId equals t.TopicId
                                           into joined

                                           from j in joined
                                           select j).FirstOrDefaultAsync();
                }
            }

            if (_currentTopic == null)
            {
                return NotFound($"Mesajul {postId} nu există.");
            }
            var usr = await GetCurrentUser();
            var pageSize = usr.TopicPostsPerPage.ContainsKey(_currentTopic.TopicId) ? usr.TopicPostsPerPage[_currentTopic.TopicId] : 14;
            PostsPerPage.ForEach(ppp => ppp.Selected = int.TryParse(ppp.Value, out var value) && value == pageSize);

            GetPostsLazy(_currentTopic.TopicId);
            var index = _dbPosts.Select(p => p.PostId).ToList().IndexOf(postId) + 1;
            var pageNum = (index / pageSize) + (index % pageSize == 0 ? 0 : 1);

            PostId = postId;
            Highlight = highlight;
            return await OnGet(_currentTopic.TopicId, pageNum);
        }

        public async Task<IActionResult> OnGet(int topicId, int pageNum)
        {
            PhpbbForums parent = null;
            using (var _dbContext = new forumContext(_config))
            {
                if (_currentTopic == null)
                {
                    _currentTopic = await (from t in _dbContext.PhpbbTopics
                                           where t.TopicId == topicId
                                           select t).FirstOrDefaultAsync();
                }

                if (_currentTopic == null)
                {
                    return NotFound($"Subiectul {topicId} nu există.");
                }

                parent = await (from f in _dbContext.PhpbbForums

                                join t in _dbContext.PhpbbTopics
                                on f.ForumId equals t.ForumId
                                into joined

                                from j in joined
                                where j.TopicId == topicId
                                select f).FirstOrDefaultAsync();
            }

            var usr = await GetCurrentUser();
            var pageSize = usr.TopicPostsPerPage.ContainsKey(topicId) ? usr.TopicPostsPerPage[topicId] : 14;
            PostsPerPage.ForEach(ppp => ppp.Selected = int.TryParse(ppp.Value, out var value) && value == pageSize);
            ForumId = parent?.ForumId;

            if (!string.IsNullOrEmpty(parent.ForumPassword) &&
                (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != ForumId)
            {
                if ((await GetCurrentUser()).UserPermissions.Any(fp => fp.ForumId == ForumId && fp.AuthRoleId == 16))
                {
                    return RedirectToPage("Unauthorized");
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

            GetPostsLazy(topicId);
            var noOfPages = (_dbPosts.Count() / pageSize) + (_dbPosts.Count() % pageSize == 0 ? 0 : 1);
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

            using (var _dbContext = new forumContext(_config))
            {
                var postsInPage = (from p in _dbPosts.Skip((pageNum - 1) * pageSize).Take(pageSize)
                                   join u in _dbContext.PhpbbUsers
                                   on p.PosterId equals u.UserId
                                   into joinedUsers

                                   join a in _dbContext.PhpbbAttachments
                                   on p.PostId equals a.PostMsgId
                                   into joinedAttachments

                                   from ju in joinedUsers.DefaultIfEmpty()

                                   select new PostDisplay
                                   {
                                       PostTitle = p.PostSubject,
                                       PostText = p.PostText,
                                       AuthorName = ju == null ? "Anonymous" : (ju.UserId == 1 ? p.PostUsername : ju.Username),
                                       AuthorId = ju == null ? 1 : (ju.UserId == 1 ? null as int? : ju.UserId),
                                       PostCreationTime = p.PostTime.TimestampToLocalTime(),
                                       PostModifiedTime = p.PostEditTime.TimestampToLocalTime(),
                                       Id = p.PostId,
                                       Attachments = (from ja in joinedAttachments
                                                      select ja.ToModel()).ToList(),
                                       BbcodeUid = p.BbcodeUid
                                   }).ToList();


                var bbcodes = new List<BBTag>(from c in _dbContext.PhpbbBbcodes
                                              select new BBTag(c.BbcodeTag, c.BbcodeTpl, string.Empty, false, false));
                bbcodes.AddRange(new[]
                {
                    new BBTag("b", "<b>", "</b>"),
                    new BBTag("i", "<span style=\"font-style:italic;\">", "</span>"),
                    new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>"),
                    new BBTag("code", "<pre class=\"prettyprint\">", "</pre>"),
                    new BBTag("img", "<br/><img src=\"${content}\" /><br/>", string.Empty, false, false),
                    new BBTag("quote", "<blockquote>${name}", "</blockquote>",
                        new BBAttribute("name", "", (a) => string.IsNullOrWhiteSpace(a.AttributeValue) ? "" : $"<b>{HttpUtility.HtmlDecode(a.AttributeValue).Trim('"')}</b> a scris:<br/>")),
                    new BBTag("*", "<li>", "</li>", true, false),
                    new BBTag("list", "<${attr}>", "</${attr}>", true, true,
                        new BBAttribute("attr", "", a => string.IsNullOrWhiteSpace(a.AttributeValue) ? "ul" : $"ol type=\"{a.AttributeValue}\"")),
                    new BBTag("url", "<a href=\"${href}\">", "</a>",
                        new BBAttribute("href", "", a => string.IsNullOrWhiteSpace(a?.AttributeValue) ? "${content}" : a.AttributeValue)),
                    new BBTag("color", "<span style=\"color:${code}\">", "</span>",
                        new BBAttribute("code", ""),
                        new BBAttribute("code", "code")),
                    new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>",
                        new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")),
                    new BBTag("attachment", "##AttachmentFileName=${content}##", "", false, true,
                        new BBAttribute("num", ""),
                        new BBAttribute("num", "num"))
                });
                var parser = new BBCodeParser(bbcodes);
                var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, _AttachmentPartialModel Attach)>();
                var htmlCommentRegex = new Regex("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
                var newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);

                var smileyRegex = new Regex("{SMILIES_PATH}", RegexOptions.Compiled | RegexOptions.Singleline);

                Parallel.ForEach(postsInPage, (p, state1) =>
                {
                    p.PostTitle = HttpUtility.HtmlDecode(p.PostTitle);
                    p.PostText = HttpUtility.HtmlDecode(parser.ToHtml(p.PostText, p.BbcodeUid));
                    p.PostText = newLineRegex.Replace(p.PostText, "<br/>");
                    p.PostText = htmlCommentRegex.Replace(p.PostText, string.Empty);
                    p.PostText = smileyRegex.Replace(p.PostText, Constants.SMILEY_PATH);

                    Parallel.ForEach(p.Attachments, (candidate, state2) =>
                    {
                        if (p.PostText.Contains($"##AttachmentFileName={candidate.FileName}##"))
                        {
                            inlineAttachmentsPosts.Add((p.Id.Value, candidate));
                        }
                    });
                });
                Posts = postsInPage.ToList();
                Posts.ForEach(async (p) =>
                {
                    foreach (var candidate in from a in inlineAttachmentsPosts
                                              where a.PostId == p.Id
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
                    _dbContext.PhpbbPosts.Count(p => p.TopicId == topicId),
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
                var usr = await GetCurrentUser();
                var curValue = await context.PhpbbUserTopicPostNumber
                                               .FirstOrDefaultAsync(ppp => ppp.UserId == usr.UserId &&
                                                                           ppp.TopicId == topicId);

                if (curValue == null)
                {
                    context.PhpbbUserTopicPostNumber
                              .Add(new PhpbbUserTopicPostNumber
                              {
                                  UserId = usr.UserId.Value,
                                  TopicId = topicId,
                                  PostNo = userPostsPerPage
                              });
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

        private void GetPostsLazy(int topicId)
        {
            if (_dbPosts == null)
            {
                using (var _dbContext = new forumContext(_config))
                {
                    _dbPosts = Utils.Instance.GetPosts(topicId, _dbContext).ToList();
                }
            }
        }
    }
}