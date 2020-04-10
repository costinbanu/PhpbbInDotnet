using CodeKicker.BBCode.Core;
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
        private readonly WritingToolsService _writingService;
        private readonly Regex _htmlRegex;
        private readonly Regex _htmlCommentRegex;
        private readonly Regex _newLineRegex;
        private readonly Regex _smileyRegex;
        private readonly Regex _tabRegex;
        private readonly Regex _spaceRegex;
        private BBCodeParser _parser;

        private delegate (int index, string match) FirstIndexOf(string haystack, string needle, int startIndex);
        private delegate (string result, int endIndex) Transform(string haystack, string needle, int startIndex);

        public PostService(IConfiguration config, Utils utils, UserService userService, WritingToolsService writingService)
        {
            _config = config;
            _utils = utils;
            _userService = userService;
            _writingService = writingService;
            _htmlCommentRegex = new Regex("(<!--.*?-->)|(&lt;!--.*?--&gt;)", RegexOptions.Compiled | RegexOptions.Singleline);
            _newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);
            _tabRegex = new Regex("\t", RegexOptions.Compiled | RegexOptions.Singleline);
            _spaceRegex = new Regex(" +", RegexOptions.Compiled | RegexOptions.Singleline);
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
            var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, int AttachIndex, _AttachmentPartialModel Attach)>();
            var attachRegex = new Regex("#{AttachmentFileName=[^/]+/AttachmentIndex=[0-9]+}#", RegexOptions.Compiled);
            var highlightWords = SplitHighlightWords(toHighlight);
            var bannedWords = (await _writingService.GetBannedWords()).GroupBy(p => p.Word).Select(grp => grp.FirstOrDefault()).ToDictionary(x => x.Word, y => y.Replacement);
            var locker = new object();

            Parallel.ForEach(Posts, async (p, state1) =>
            {
                p.PostSubject = CensorWords(HttpUtility.HtmlDecode(p.PostSubject), bannedWords);
                p.PostText = CensorWords(p.PostText, bannedWords);
                p.PostSubject = HighlightWords(p.PostSubject, highlightWords);
                p.PostText = HighlightWords(await BbCodeToHtml(p.PostText, p.BbcodeUid), highlightWords);

                if (renderAttachments)
                {
                    var matches = from m in attachRegex.Matches(p.PostText).AsEnumerable()
                                  where m.Success
                                  orderby m.Index descending
                                  let parts = m.Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                  let fn = parts[0].Trim("#{".ToCharArray()).Replace("AttachmentFileName=", string.Empty)
                                  let i = int.Parse(parts[1].Trim("}#".ToCharArray()).Replace("AttachmentIndex=", string.Empty))
                                  select (FileName: fn, AttachIndex: i, Original: m);

                    Parallel.ForEach(matches, (m, state2) =>
                    {
                        _AttachmentPartialModel model = null;
                        int index = m.AttachIndex;
                        var candidates = p.Attachments.Where(a => a.FileName == m.FileName).ToList();
                        if (candidates.Count == 1)
                        {
                            model = candidates.First();
                        }
                        else if(candidates.Count > 1)
                        {
                            model = candidates.FirstOrDefault(a => a.FileName == m.FileName && candidates.IndexOf(a) == index);
                            if (model == null)
                            {
                                index = candidates.Count - m.AttachIndex - 1;
                                model = candidates.FirstOrDefault(a => candidates.IndexOf(a) == index);
                            }
                        }

                        if (model != null)
                        {
                            lock (locker)
                            {
                                p.PostText = p.PostText.Replace(
                                    $"#{{AttachmentFileName={model.FileName}/AttachmentIndex={index}}}#",
                                    _utils.RenderRazorViewToString("_AttachmentPartial", model, pageContext, httpContext).RunSync()
                                );
                                p.Attachments.Remove(model);
                            }
                        }
                    });
                }
                else
                {
                    p.PostText = attachRegex.Replace(p.PostText, string.Empty);
                }
            });
        }

        public async Task<string> BbCodeToHtml(string bbCodeText, string bbCodeUid)
        {           
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            bbCodeText = (await GetParserLazy()).ToHtml(bbCodeText, bbCodeUid);
            bbCodeText = _newLineRegex.Replace(bbCodeText, "<br/>");
            bbCodeText = _htmlCommentRegex.Replace(bbCodeText, string.Empty);
            bbCodeText = _smileyRegex.Replace(bbCodeText, Constants.SMILEY_PATH);
            bbCodeText = _tabRegex.Replace(bbCodeText, _utils.HtmlSafeWhitespace(4));

            var offset = 0;
            foreach (Match m in _spaceRegex.Matches(bbCodeText))
            {
                var (result, curOffset) = _utils.ReplaceAtIndex(bbCodeText, m.Value, _utils.HtmlSafeWhitespace(m.Length), m.Index + offset);
                bbCodeText = result;
                offset += curOffset;
            }

            return HttpUtility.HtmlDecode(bbCodeText);
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

        public string CleanTextForQuoting(string text, string uid)
        {
            var uidRegex = new Regex($":{uid}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var tagRegex = new Regex(@"(:[a-z])(\]|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var cleanTextTemp = uidRegex.Replace(text, string.Empty);
            var noUid = tagRegex.Replace(cleanTextTemp, "$2");

            var noSmileys = noUid;
            var smileyRegex = new Regex("<!-- s(:?.+?) -->.+?<!-- s:?.+?:? -->", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var smileyMatches = smileyRegex.Matches(noSmileys);
            try
            {
                foreach (Match m in smileyMatches)
                {
                    noSmileys = noSmileys.Replace(m.Value, m.Groups[1].Value);
                }
            }
            catch { }

            var noLinks = noSmileys;
            var linkRegex = new Regex(@"<!-- m --><a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1.+?<!-- m -->", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var linkMatches = linkRegex.Matches(noLinks);
            try
            {
                foreach (Match m in linkMatches)
                {
                    noLinks = noLinks.Replace(m.Value, m.Groups[2].Value);
                }
            }
            catch { }

            return noLinks;
        }

        public string PrepareTextForSaving(ForumDbContext context, string text)
        {
            foreach (var sr in from s in context.PhpbbSmilies.AsNoTracking()
                               select new
                               {
                                   Regex = new Regex(Regex.Escape(s.Code), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                                   Replacement = $"<!-- s{s.Code} --><img src=\"./images/smilies/{s.SmileyUrl.Trim('/')}\" /><!-- s{s.Code} -->"
                               }) 
            {
                text = sr.Regex.Replace(text, sr.Replacement);
            }

            var urlRegex = new Regex(@"(ftp:\/\/|www\.|https?:\/\/){1}[a-zA-Z0-9u00a1-\uffff0-]{2,}\.[a-zA-Z0-9u00a1-\uffff0-]{2,}(\S*)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
            var offset = 0;
            foreach (Match match in urlRegex.Matches(text))
            {
                var linkText = match.Value;
                if (linkText.Length > 53)
                {
                    linkText = $"{linkText.Substring(0, 40)} ... {linkText.Substring(linkText.Length - 8)}";
                }
                var (result, curOffset) = _utils.ReplaceAtIndex(text, match.Value, match.Result($"<!-- m --><a href=\"{match.Value}\">{linkText}</a><!-- m -->"), match.Index + offset);
                text = result;
                offset += curOffset;
            }
            return text;
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
                        new BBAttribute("code", "")),
                    new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>",
                        new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")),
                    new BBTag("attachment", "#{AttachmentFileName=${content}/AttachmentIndex=${num}}#", "", false, true,
                        new BBAttribute("num", ""))
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

        private string HighlightWords(string text, List<string> words)
            => ProcessAllWords(
                text, 
                words, 
                false, 
                (haystack, needle, startIndex) => (haystack.IndexOf(needle, startIndex, StringComparison.CurrentCultureIgnoreCase), needle),
                (haystack, needle, index) =>
                {
                    var openTag = "<span class=\"posthilit\">";
                    var closeTag = "</span>";
                    haystack = haystack.Insert(index, openTag);
                    haystack = haystack.Insert(index + openTag.Length + needle.Length, closeTag);
                    return (haystack, index + openTag.Length + needle.Length + closeTag.Length);
                }
            );

        private string CensorWords(string text, Dictionary<string, string> wordMap)
        {
            Regex getRegex(string wildcard)
                => new Regex(@"\b" + Regex.Escape(wildcard).Replace(@"\*", @"\w*").Replace(@"\?", @"\w") + @"\b");

            return ProcessAllWords(
                text, 
                wordMap.Keys, 
                false,
                (haystack, needle, startIndex) =>
                {
                    var match = getRegex(needle).Match(haystack, startIndex);
                    return match.Success ? (match.Index, match.Value) : (-1, needle);
                },
                (haystack, needle, index) =>
                {
                    var replacement = wordMap.Select(x => KeyValuePair.Create(getRegex(x.Key), x.Value)).FirstOrDefault(x => x.Key.IsMatch(needle)).Value;
                    if (replacement == null)
                    {
                        return (haystack, index + needle.Length);
                    }
                    var (result, offset) = _utils.ReplaceAtIndex(haystack, needle, replacement, index);
                    return (result, index + replacement.Length);
                }
            );
        }

        private string ProcessAllWords(string input, IEnumerable<string> words, bool expectHtmlInInput, FirstIndexOf indexOf, Transform transform)
        {
            var htmlTagsLocation = new List<(int Position, int Length)>();
            var cleanedInput = string.Empty;
            var shouldProcess = words.Any();

            if (shouldProcess)
            {
                cleanedInput = input.RemoveDiacritics();
                if (cleanedInput.Length == input.Length && expectHtmlInInput)
                {
                    foreach (Match m in _htmlRegex.Matches(cleanedInput))
                    {
                        htmlTagsLocation.Add((m.Groups[0].Index, m.Groups[0].Length));
                    }
                }
                else if (cleanedInput.Length != input.Length)
                {
                    shouldProcess = false;
                }
            }

            if (shouldProcess)
            {
                foreach (var word in words)
                {
                    var cleanedWord = word.RemoveDiacritics();
                    if (cleanedWord.Length == word.Length)
                    {
                        var (index, cleanedMatch) = indexOf(cleanedInput, cleanedWord, 0);
                        if(index == -1)
                        {
                            continue;
                        }

                        var match = input.Substring(index, cleanedMatch.Length);

                        if(match.RemoveDiacritics() != cleanedMatch)
                        {
                            continue;
                        }

                        while (index != -1)
                        {
                            var startIndex = 0;
                            var tag = htmlTagsLocation.FirstOrDefault(x => index >= x.Position && index < x.Position + x.Length);
                            if (tag == default)
                            {
                                var (result, nextIndex) = transform(input, match, index);
                                var (cleanedResult, cleanednextIndex) = transform(cleanedInput, cleanedMatch, index);
                                if (result.RemoveDiacritics() == cleanedResult && nextIndex == cleanednextIndex)
                                {
                                    input = result;
                                    cleanedInput = cleanedResult;
                                    startIndex = nextIndex;
                                }
                            }
                            else
                            {
                                startIndex = tag.Position + tag.Length;
                            }
                            index = startIndex > cleanedInput.Length ? -1 : indexOf(cleanedInput, cleanedWord, startIndex).index;
                        }
                    }
                }
            }
            return input;
        }
    }
}
