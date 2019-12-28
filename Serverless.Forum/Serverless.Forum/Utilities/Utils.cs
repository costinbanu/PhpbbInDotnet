using CodeKicker.BBCode.Core;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
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
using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        public readonly ClaimsPrincipal Anonymous;

        private readonly Regex _htmlCommentRegex;
        private readonly Regex _newLineRegex;
        private readonly Regex _smileyRegex;
        private readonly IConfiguration _config;
        private readonly BBCodeParser _parser;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpContext _context;
        private readonly ITempDataProvider _tempDataProvider;

        public Utils(IConfiguration config, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider, IHttpContextAccessor accessor)
        {
            _config = config;
            using (var context = new ForumDbContext(_config))
            {
                Anonymous = LoggedUserFromDbUserAsync(context.PhpbbUsers.First(u => u.UserId == 1u)).GetAwaiter().GetResult();

                _htmlCommentRegex = new Regex("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
                _newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);
                _smileyRegex = new Regex("{SMILIES_PATH}", RegexOptions.Compiled | RegexOptions.Singleline);

                _viewEngine = viewEngine;
                _tempDataProvider = tempDataProvider;
                _serviceProvider = serviceProvider;
                _context = accessor?.HttpContext;

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
            }
        }

        public async Task<ClaimsPrincipal> LoggedUserFromDbUserAsync(PhpbbUsers user)
        {
            using (var context = new ForumDbContext(_config))
            using (var connection = context.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                DefaultTypeMap.MatchNamesWithUnderscores = true;

                using (var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_user_details`(@UserId);", new { user.UserId }))
                {
                    var intermediary = new LoggedUser
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        UsernameClean = user.UsernameClean,
                        UserPermissions = await multi.ReadAsync<LoggedUser.Permissions>(),
                        Groups = (await multi.ReadAsync<uint>()).Select(x => checked((int)x)),
                        TopicPostsPerPage = (await multi.ReadAsync()).ToDictionary(key => checked((int)key.topic_id), value => checked((int)value.post_no)),
                        UserDateFormat = user.UserDateformat
                    };

                    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
                    identity.AddClaim(new Claim(ClaimTypes.UserData, await CompressObjectAsync(intermediary)));
                    return new ClaimsPrincipal(identity);
                }
            }
        }

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

        public void ProcessPosts(List<PostDisplay> Posts, PageContext pageContext, bool renderAttachments)
        {
            var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, _AttachmentPartialModel Attach)>();
            var attachRegex = new Regex("##AttachmentFileName=.*##", RegexOptions.Compiled);

            Parallel.ForEach(Posts, (p, state1) =>
            {
                p.PostSubject = HttpUtility.HtmlDecode(p.PostSubject);
                p.PostText = HttpUtility.HtmlDecode(_parser.ToHtml(p.PostText, p.BbcodeUid));
                p.PostText = _newLineRegex.Replace(p.PostText, "<br/>");
                p.PostText = _htmlCommentRegex.Replace(p.PostText, string.Empty);
                p.PostText = _smileyRegex.Replace(p.PostText, Constants.SMILEY_PATH);
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
                Posts.ForEach(async (p) =>
                {
                    foreach (var (PostId, Attach) in from a in inlineAttachmentsPosts
                                                     where a.PostId == p.PostId
                                                     select a)
                    {
                        p.PostText = p.PostText.Replace(
                            $"##AttachmentFileName={Attach.FileName}##",
                            await RenderRazorViewToString("_AttachmentPartial", Attach, pageContext)
                        );
                        p.Attachments.Remove(Attach);
                    }
                });
            }
        }

        public async Task<string> CompressObjectAsync<T>(T source)
        {
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source))))
            using (var memory = new MemoryStream())
            using (var gzip = new GZipStream(memory, CompressionMode.Compress))
            {
                await content.CopyToAsync(gzip);
                await gzip.FlushAsync();
                return Convert.ToBase64String(memory.ToArray());
            }
        }

        public async Task<T> DecompressObjectAsync<T>(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return default;
            }
            using (var content = new MemoryStream())
            using (var memory = new MemoryStream(Convert.FromBase64String(source)))
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

        private async Task<string> RenderRazorViewToString(string viewName, _AttachmentPartialModel model, PageContext pageContext)
        {
            try
            {
                var actionContext = new ActionContext(_context, _context.GetRouteData(), pageContext.ActionDescriptor);
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
    }
}
