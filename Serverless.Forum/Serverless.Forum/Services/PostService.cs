﻿using CodeKicker.BBCode.Core;
using Dapper;
using Diacritics.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Services
{
    public class PostService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;
        private readonly UserService _userService;
        private readonly Regex _htmlRegex;
        private readonly Regex _htmlCommentRegex;
        private readonly Regex _newLineRegex;
        private readonly Regex _smileyRegex;
        private BBCodeParser _parser;

        public PostService(IConfiguration config, Utils utils, UserService userService)
        {
            _config = config;
            _utils = utils;
            _userService = userService;
            _htmlCommentRegex = new Regex("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
            _newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);
            _smileyRegex = new Regex("{SMILIES_PATH}", RegexOptions.Compiled | RegexOptions.Singleline);
            _htmlRegex = new Regex(@"<((?=!\-\-)!\-\-[\s\S]*\-\-|((?=\?)\?[\s\S]*\?|((?=\/)\/[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*|[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*(?:\s[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*(?:=(?:""[^""]*""|'[^']*'|[^'""<\s]*))?)*)\s?\/?))>", RegexOptions.Compiled);
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
                    var toReturn = (
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

        public async Task ProcessPosts(IEnumerable<PostDisplay> Posts, PageContext pageContext, HttpContext httpContext, bool renderAttachments, string toHighlight = null)
        {
            var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, _AttachmentPartialModel Attach)>();
            var attachRegex = new Regex("##AttachmentFileName=.*##", RegexOptions.Compiled);
            var highlightWords = SplitHighlightWords(toHighlight);

            Parallel.ForEach(Posts, async (p, state1) =>
            {

                p.PostSubject = HighLightText(HttpUtility.HtmlDecode(p.PostSubject), highlightWords, false);
                p.PostText = HighLightText(await BbCodeToHtml(p.PostText, p.BbcodeUid), highlightWords, false);

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
                            await _utils.RenderRazorViewToString("_AttachmentPartial", Attach, pageContext, httpContext)
                        );
                        p.Attachments.Remove(Attach);
                    }
                }
            }
        }

        public async Task<string> BbCodeToHtml(string bbCodeText, string bbCodeUid)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            bbCodeText = HttpUtility.HtmlDecode((await GetParserLazy()).ToHtml(bbCodeText, bbCodeUid));
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
                var lastPostUser = await _userService.DbUserToLoggedUserAsync(
                    await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == lastPost.PosterId)
                );

                curTopic.TopicLastPostId = lastPost.PosterId;
                curTopic.TopicLastPostSubject = lastPost.PostSubject;
                curTopic.TopicLastPostTime = lastPost.PostTime;
                curTopic.TopicLastPosterColour = lastPostUser.UserColor;
                curTopic.TopicLastPosterName = lastPostUser == await _userService.GetAnonymousLoggedUserAsync() ? lastPost.PostUsername : lastPostUser.Username;
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
                var lastPostUser = await _userService.DbUserToLoggedUserAsync(
                    await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == lastPost.PosterId)
                );

                curForum.ForumLastPostId = lastPost.PosterId;
                curForum.ForumLastPostSubject = lastPost.PostSubject;
                curForum.ForumLastPostTime = lastPost.PostTime;
                curForum.ForumLastPosterColour = lastPostUser.UserColor;
                curForum.ForumLastPosterName = lastPostUser == await _userService.GetAnonymousLoggedUserAsync() ? lastPost.PostUsername : lastPostUser.Username;
            }
        }

        private async Task<BBCodeParser> GetParserLazy()
        {
            if (_parser != null)
            {
                return _parser;
            }
            using (var context = new ForumDbContext(_config))
            {
                var bbcodes = await context.PhpbbBbcodes.AsNoTracking().Select(c => new BBTag(c.BbcodeTag, c.BbcodeTpl, string.Empty, false, false)).ToListAsync();


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
                return _parser;
            }
        }

        private List<string> SplitHighlightWords(string search)
        {
            var highlightWords = new List<string>();
            if (string.IsNullOrWhiteSpace(search))
            {
                return highlightWords;
            }

            var sb = new StringBuilder();
            var openQuote = false;
            foreach (var ch in search)
            {
                if ((char.IsLetterOrDigit(ch) || openQuote) && ch != '"')
                {
                    sb.Append(ch);
                }
                else if (sb.Length > 0)
                {
                    highlightWords.Add(sb.ToString());
                    sb.Clear();
                }
                if (ch == '"')
                {
                    openQuote = !openQuote;
                }
            }
            if (sb.Length > 0)
            {
                highlightWords.Add(sb.ToString());
                sb.Clear();
            }
            return highlightWords;
        }

        private string HighLightText(string input, List<string> highlightWords, bool expectHtmlInInput)
        {
            var htmlTagsLocation = new List<(int Position, int Length)>();
            var cleanText = string.Empty;
            var shouldHighlight = highlightWords.Any();

            if (shouldHighlight)
            {
                cleanText = input.RemoveDiacritics();
                if (cleanText.Length == input.Length && expectHtmlInInput)
                {
                    var m = _htmlRegex.Match(cleanText);
                    while (m.Success)
                    {
                        htmlTagsLocation.Add((m.Groups[0].Index, m.Groups[0].Length));
                        m = m.NextMatch();
                    }
                }
                else if (cleanText.Length != input.Length)
                {
                    shouldHighlight = false;
                }
            }

            if (shouldHighlight)
            {
                foreach (var word in highlightWords)
                {
                    var cleanWord = word.RemoveDiacritics();
                    if (cleanWord.Length == word.Length)
                    {
                        var index = cleanText.IndexOf(cleanWord, 0, StringComparison.CurrentCultureIgnoreCase);
                        while (index != -1)
                        {
                            var startIndex = 0;
                            var tag = htmlTagsLocation.FirstOrDefault(x => index >= x.Position && index < x.Position + x.Length);
                            if (tag == default)
                            {
                                var openTag = "<span class=\"posthilit\">";
                                var closeTag = "</span>";
                                input = input.Insert(index, openTag);
                                input = input.Insert(index + openTag.Length + word.Length, closeTag);
                                startIndex = index + openTag.Length + word.Length + closeTag.Length;
                            }
                            else
                            {
                                startIndex = tag.Position + tag.Length;
                            }
                            index = startIndex > cleanText.Length ? -1 : cleanText.IndexOf(cleanWord, startIndex, StringComparison.CurrentCultureIgnoreCase);
                        }
                    }
                }
            }
            return input;
        }
    }
}
