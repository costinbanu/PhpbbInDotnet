using CodeKicker.BBCode.Core;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Utilities
{
    public class Utils
    {
        public readonly ClaimsPrincipal AnonymousClaimsPrincipal;
        public readonly PhpbbUsers AnonymousDbUser;
        public readonly LoggedUser AnonymousLoggedUser;

        private readonly Regex _htmlCommentRegex;
        private readonly Regex _newLineRegex;
        private readonly Regex _smileyRegex;
        private readonly IConfiguration _config;
        private readonly ILogger<Utils> _logger;
        private readonly BBCodeParser _parser;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IDistributedCache _cache;

        private readonly List<PhpbbAclRoles> _adminRoles;
        private readonly List<PhpbbAclRoles> _modRoles;

        public Utils(IConfiguration config, IDistributedCache cache, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, ILogger<Utils> logger)
        {
            _config = config;
            _logger = logger;
            _htmlCommentRegex = new Regex("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
            _newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);
            _smileyRegex = new Regex("{SMILIES_PATH}", RegexOptions.Compiled | RegexOptions.Singleline);
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _cache = cache;

            using (var context = new ForumDbContext(_config))
            {
                AnonymousDbUser = context.PhpbbUsers.First(u => u.UserId == 1);
                AnonymousClaimsPrincipal = AnonymousDbUser.ToClaimsPrincipalAsync(context, this).RunSync();
                AnonymousLoggedUser = AnonymousClaimsPrincipal.ToLoggedUserAsync(this).RunSync();

                var bbcodes = new List<BBTag>(from c in context.PhpbbBbcodes
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
                _parser = new BBCodeParser(bbcodes);

                _adminRoles = (from r in context.PhpbbAclRoles
                               where r.RoleType == "a_"
                               select r).ToList();

                _modRoles = (from r in context.PhpbbAclRoles
                             where r.RoleType == "m_"
                             select r).ToList();
            }
        }

        #region Posts

        public async Task<(List<PhpbbPosts> Posts, int Page, int Count)> GetPostPageAsync(int userId, int? topicId, int? page, int? postId)
        {
            using (var context = new ForumDbContext(_config))
            using (var connection = context.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                DefaultTypeMap.MatchNamesWithUnderscores = true;

                using (var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_posts`(@userId, @topicId, @page, @postId);", new { userId, topicId, page, postId }))
                {
                    var toReturn =  (
                        Posts: multi.Read<PhpbbPosts>().ToList(),
                        Page: multi.Read<int>().Single(),
                        Count: multi.Read<int>().Single()
                    );

                    var attachments = from a in context.PhpbbAttachments

                                      join p in toReturn.Posts
                                      on a.PostMsgId equals p.PostId

                                      select new PhpbbAttachments
                                      {
                                          AttachComment = a.AttachComment,
                                          AttachId = a.AttachId,
                                          DownloadCount = a.DownloadCount + 1,
                                          Extension = a.Extension,
                                          Filesize = a.Filesize,
                                          Filetime = a.Filetime,
                                          InMessage = a.InMessage,
                                          IsOrphan = a.IsOrphan,
                                          Mimetype = a.Mimetype,
                                          PhysicalFilename = a.PhysicalFilename,
                                          PosterId = a.PosterId,
                                          PostMsgId = a.PostMsgId,
                                          RealFilename = a.RealFilename,
                                          Thumbnail = a.Thumbnail,
                                          TopicId = a.TopicId
                                      };

                    context.PhpbbAttachments.UpdateRange(attachments);
                    await context.SaveChangesAsync();

                    return toReturn;
                }
            }
        }

        public async Task ProcessPosts(IEnumerable<PostDisplay> Posts, PageContext pageContext, HttpContext httpContext, bool renderAttachments)
        {
            var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, _AttachmentPartialModel Attach)>();
            var attachRegex = new Regex("##AttachmentFileName=.*##", RegexOptions.Compiled);

            Parallel.ForEach(Posts, (p, state1) =>
            {
                p.PostSubject = HttpUtility.HtmlDecode(p.PostSubject);
                p.PostText = BbCodeToHtml(p.PostText, p.BbcodeUid);
                if (renderAttachments)
                {
                    Parallel.ForEach(p.Attachments, (candidate, state2) =>
                    {
                        if (p.PostText.Contains($"##AttachmentFileName={candidate.FileName}##"))
                        {
                            inlineAttachmentsPosts.Add((p.PostId.Value, candidate));
                        }
                    });
                }
                else
                {
                    p.PostText = attachRegex.Replace(p.PostText, string.Empty);
                }
            });

            if (renderAttachments)
            {
                foreach (var p in Posts)
                {
                    foreach (var (PostId, Attach) in from a in inlineAttachmentsPosts
                                                     where a.PostId == p.PostId
                                                     select a)
                    {
                        p.PostText = p.PostText.Replace(
                            $"##AttachmentFileName={Attach.FileName}##",
                            await RenderRazorViewToString("_AttachmentPartial", Attach, pageContext, httpContext)
                        );
                        p.Attachments.Remove(Attach);
                    }
                }
            }
        }

        public string BbCodeToHtml(string bbCodeText, string bbCodeUid)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            bbCodeText = HttpUtility.HtmlDecode(_parser.ToHtml(bbCodeText, bbCodeUid));
            bbCodeText = _newLineRegex.Replace(bbCodeText, "<br/>");
            bbCodeText = _htmlCommentRegex.Replace(bbCodeText, string.Empty);
            bbCodeText = _smileyRegex.Replace(bbCodeText, Constants.SMILEY_PATH);

            return bbCodeText;
        }

        public async Task CascadePostAdd(ForumDbContext context, PhpbbPosts added, LoggedUser usr)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == added.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            curTopic.TopicLastPosterColour = curForum.ForumLastPosterColour = usr.UserColor;
            curTopic.TopicLastPosterId = curForum.ForumLastPosterId = usr.UserId;
            curTopic.TopicLastPosterName = curForum.ForumLastPosterName = HttpUtility.HtmlEncode(usr.Username);
            curTopic.TopicLastPostId = curForum.ForumLastPostId = added.PostId;
            curTopic.TopicLastPostSubject = curForum.ForumLastPostSubject = HttpUtility.HtmlEncode(added.PostSubject);
            curTopic.TopicLastPostTime = curForum.ForumLastPostTime = added.PostTime;
        }

        public async Task CascadePostDelete(ForumDbContext context, PhpbbPosts deleted)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == deleted.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            if (context.PhpbbPosts.Count(p => p.TopicId == deleted.TopicId) == 0)
            {
                context.PhpbbTopics.Remove(curTopic);
            }
            else if (curTopic.TopicLastPostId == deleted.PostId)
            {
                var lastPost = await (
                    from p in context.PhpbbPosts
                    where p.TopicId == deleted.TopicId
                    group p by p.PostTime into grouped
                    orderby grouped.Key descending
                    select grouped.FirstOrDefault()
                ).FirstOrDefaultAsync();
                var lastPostUser = await (await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == lastPost.PosterId)).ToLoggedUserAsync(context, this);

                curTopic.TopicLastPostId = lastPost.PosterId;
                curTopic.TopicLastPostSubject = lastPost.PostSubject;
                curTopic.TopicLastPostTime = lastPost.PostTime;
                curTopic.TopicLastPosterColour = lastPostUser.UserColor;
                curTopic.TopicLastPosterName = lastPostUser == AnonymousLoggedUser ? lastPost.PostUsername : lastPostUser.Username;
            }

            if (curForum.ForumLastPostId == deleted.PostId)
            {
                var lastPost = await (
                    from t in context.PhpbbTopics
                    where t.ForumId == curForum.ForumId
                    join p in context.PhpbbPosts
                    on t.TopicId equals p.TopicId
                    group p by p.PostTime into grouped
                    orderby grouped.Key descending
                    select grouped.FirstOrDefault()
                ).FirstOrDefaultAsync();
                var lastPostUser = await (await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == lastPost.PosterId)).ToLoggedUserAsync(context, this);
                
                curForum.ForumLastPostId = lastPost.PosterId;
                curForum.ForumLastPostSubject = lastPost.PostSubject;
                curForum.ForumLastPostTime = lastPost.PostTime;
                curForum.ForumLastPosterColour = lastPostUser.UserColor;
                curForum.ForumLastPosterName = lastPostUser == AnonymousLoggedUser ? lastPost.PostUsername : lastPostUser.Username;
            }
        }

        #endregion Posts

        #region Generics

        public async Task<byte[]> CompressObjectAsync<T>(T source)
        {
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source))))
            using (var memory = new MemoryStream())
            using (var gzip = new GZipStream(memory, CompressionMode.Compress))
            {
                await content.CopyToAsync(gzip);
                await gzip.FlushAsync();
                return memory.ToArray();
            }
        }

        public async Task<T> DecompressObjectAsync<T>(byte[] source)
        {
            if (!(source?.Any() ?? false))
            {
                return default;
            }

            using (var content = new MemoryStream())
            using (var memory = new MemoryStream(source))
            using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
            {
                await gzip.CopyToAsync(content);
                await content.FlushAsync();
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(content.ToArray()));
            }
        }

        public string RandomString(int length = 8)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        public string CalculateMD5Hash(string input)
        {
            var hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public string CleanString(string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().ToLower().Normalize(NormalizationForm.FormC);
        }

        public async Task SendEmail(MailMessage emailMessage)
        {
            using (var smtp = new SmtpClient(_config.GetValue<string>("Smtp:Host"), _config.GetValue<int>("Smtp:Post"))
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential
                {
                    UserName = _config.GetValue<string>("Smtp:Username"),
                    Password = _config.GetValue<string>("Smtp:Password")
                }
            })
            {
                await smtp.SendMailAsync(emailMessage);
            }
        }

        public async Task<string> RenderRazorViewToString(string viewName, PageModel model, PageContext pageContext, HttpContext httpContext)
        {
            try
            {
                var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), pageContext.ActionDescriptor);
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
                        RouteData = httpContext.GetRouteData()
                    };

                    await viewResult.View.RenderAsync(viewContext);
                    return sw.GetStringBuilder().ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error rendering partial view.");
                return string.Empty;
            }
        }


        #endregion Generics

        #region Caching

        public async Task<T> GetFromCacheAsync<T>(string key)
            => await DecompressObjectAsync<T>(await _cache.GetAsync(key));

        public async Task SetInCacheAsync<T>(string key, T value)
            => await _cache.SetAsync(
                key,
                await CompressObjectAsync(value),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) }
            );

        public async Task<bool> ExistsInCacheAsync(string key)
            => (await _cache.GetAsync(key))?.Any() ?? false;

        public async Task RemoveFromCacheAsync(string key)
            => await _cache.RemoveAsync(key);

        #endregion Caching

        #region Users

        public bool IsUserAdminInForum(LoggedUser user, int forumId)
            => user == null || (from up in user.UserPermissions
                                where up.ForumId == forumId || up.ForumId == 0
                                join a in _adminRoles
                                on up.AuthRoleId equals a.RoleId
                                select up).Any();

        public bool IsUserModeratorInForum(LoggedUser user, int forumId)
            => user == null || (from up in user.UserPermissions
                                where up.ForumId == forumId || up.ForumId == 0
                                join a in _modRoles
                                on up.AuthRoleId equals a.RoleId
                                select up).Any();

        #endregion Users
    }
}
