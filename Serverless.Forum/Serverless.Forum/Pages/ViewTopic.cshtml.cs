using CodeKicker.BBCode.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
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
        public IEnumerable<PostDisplay> Posts;
        public string TopicTitle;
        public string ForumTitle;
        public int? ForumId;
        public _PaginationPartialModel Pagination;
        public int? PostId;
        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;

        ICompositeViewEngine _viewEngine;
        PhpbbTopics _currentTopic = null;
        IEnumerable<PhpbbPosts> _dbPosts = null;

        public ViewTopicModel(forumContext context, ICompositeViewEngine viewEngine) : base(context)
        {
            _viewEngine = viewEngine;
        }

        public async Task<IActionResult> OnGetByPostId(int PostId)
        {
            if (_currentTopic == null)
            {
                _currentTopic = (from p in _dbContext.PhpbbPosts
                                 where p.PostId == PostId
                                 join t in _dbContext.PhpbbTopics
                                 on p.TopicId equals t.TopicId
                                 into joined
                                 from j in joined
                                 select j).FirstOrDefault();
            }

            if (_currentTopic == null)
            {
                return NotFound($"Mesajul {PostId} nu există.");
            }

            var pageSize = CurrentUser.TopicPostsPerPage.ContainsKey(_currentTopic.TopicId) ? CurrentUser.TopicPostsPerPage[_currentTopic.TopicId] : 14;

            GetPosts(_currentTopic.TopicId);
            var index = _dbPosts.Select(p => (int)p.PostId).ToList().IndexOf(PostId) + 1;
            var pageNum = (index / pageSize) + (index % pageSize == 0 ? 0 : 1);

            this.PostId = PostId;

            return await OnGet(_currentTopic.TopicId, pageNum).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnGet(int TopicId, int PageNum, int? pageSize = null)
        {
            if (_currentTopic == null)
            {
                _currentTopic = (from t in _dbContext.PhpbbTopics
                                 where t.TopicId == TopicId
                                 select t).FirstOrDefault();
            }

            if (_currentTopic == null)
            {
                return NotFound($"Subiectul {TopicId} nu există.");
            }

            if (pageSize is null)
            {
                pageSize = CurrentUser.TopicPostsPerPage.ContainsKey(TopicId) ? CurrentUser.TopicPostsPerPage[TopicId] : 14;
            }

            var parent = (from f in _dbContext.PhpbbForums

                          join t in _dbContext.PhpbbTopics
                          on f.ForumId equals t.ForumId
                          into joined

                          from j in joined
                          where j.TopicId == TopicId
                          select f).FirstOrDefault();

            if (!string.IsNullOrEmpty(parent.ForumPassword) &&
                (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != ForumId)
            {
                if (CurrentUser.UserPermissions.Any(fp => fp.ForumId == ForumId && fp.AuthRoleId == 16))
                {
                    return RedirectToPage("Unauthorized");
                }
                else
                {
                    return RedirectToPage("ForumLogin", new ForumLoginModel(_dbContext)
                    {
                        ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                        ForumId = parent.ForumId,
                        ForumName = parent.ForumName
                    });
                }
            }

            ForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? "untitled");
            ForumId = parent?.ForumId;

            GetPosts(TopicId);
            var noOfPages = (_dbPosts.Count() / pageSize) + (_dbPosts.Count() % pageSize == 0 ? 0 : 1);
            if (PageNum > noOfPages)
            {
                PageNum = noOfPages.Value;
            }
            if (PageNum < 1)
            {
                PageNum = 1;
            }

            var postsInPage = (from p in _dbPosts.Skip((PageNum - 1) * pageSize.Value).Take(pageSize.Value)
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
                                   Attachments = (from ja in joinedAttachments.DefaultIfEmpty()
                                                  select ja == null ? null : ja.ToModel()).ToList(),
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
                new BBTag("url", "<a href=\"${href}\">", "</a>", new BBAttribute("href", "", a => string.IsNullOrWhiteSpace(a?.AttributeValue) ? "${content}" : a.AttributeValue)),
                new BBTag("color", "<span style=\"color:${code}\">", "</span>", new BBAttribute("code", ""), new BBAttribute("code", "code")),
                new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>",
                    new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")),
                new BBTag("attachment", "##AttachmentFileName=${content}##", "", false, true, new BBAttribute("num", ""), new BBAttribute("num", "num"))
            });
            var parser = new BBCodeParser(bbcodes);
            var htmlCommentRegex = new Regex("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
            var newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);
            var notInLIneAttachments = new List<_AttachmentPartialModel>();
            var cachedAttachments = (from a in _dbContext.PhpbbAttachments
                                     join p in postsInPage
                                     on a.PostMsgId equals p.Id
                                     into joined
                                     from j in joined
                                     select a).ToList();

            foreach (var p in postsInPage)
            {
                p.PostTitle = HttpUtility.HtmlDecode(p.PostTitle);
                p.PostText = HttpUtility.HtmlDecode(parser.ToHtml(p.PostText, p.BbcodeUid));
                p.PostText =  newLineRegex.Replace(p.PostText, "<br/>");
                p.PostText = htmlCommentRegex.Replace(p.PostText, string.Empty);

                foreach(var candidate in cachedAttachments.Where(a => a.PostMsgId == p.Id))
                {
                    if (p.PostText.Contains($"##AttachmentFileName={candidate.RealFilename}##"))
                    {
                        p.PostText = p.PostText.Replace(
                            $"##AttachmentFileName={candidate.RealFilename}##",
                            await RenderRazorViewToString(
                                "_AttachmentPartial",
                                (from a in cachedAttachments
                                 where a.PostMsgId == p.Id
                                    && a.RealFilename == candidate.RealFilename
                                 select a.ToModel()).FirstOrDefault()
                            ).ConfigureAwait(false)
                        );
                    }
                    else
                    {
                        notInLIneAttachments.Add(candidate.ToModel());
                    }
                }
                p.Attachments.Clear();
                p.Attachments.AddRange(notInLIneAttachments);
                notInLIneAttachments.Clear();
            }
            Posts = postsInPage.ToList();

            TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");

            Pagination = new _PaginationPartialModel
            {
                Link = $"/ViewTopic?TopicId={TopicId}&PageNum=1",
                Posts = _dbContext.PhpbbPosts.Count(p => p.TopicId == TopicId),
                PostsPerPage = pageSize.Value,
                CurrentPage = PageNum
            };

            return Page();
        }


        private async Task<string> RenderRazorViewToString(string viewName, _AttachmentPartialModel model)
        {
            var NewViewData = new ViewDataDictionary(ViewData)
            {
                Model = model
            };

            using (var writer = new StringWriter())
            {
                ViewEngineResult viewResult =
                    _viewEngine.FindView(PageContext, viewName, false);

                ViewContext viewContext = new ViewContext(
                    PageContext,
                    viewResult.View,
                    NewViewData,
                    TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext).ConfigureAwait(false);

                return writer.GetStringBuilder().ToString();
            }
        }

        private void GetPosts(int topicId)
        {
            if (_dbPosts == null)
            {
                _dbPosts = Utils.Instance.GetPosts(topicId, _dbContext);
            }
        }
    }
}