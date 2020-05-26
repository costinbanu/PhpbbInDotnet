using CodeKicker.BBCode.Core;
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
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Services
{
    public class BBCodeRenderingService
    {
        private readonly Utils _utils;
        private readonly ForumDbContext _context;
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

        public BBCodeRenderingService(Utils utils, ForumDbContext context, WritingToolsService writingService)
        {
            _utils = utils;
            _context = context;
            _writingService = writingService;
            _htmlCommentRegex = new Regex("(<!--.*?-->)|(&lt;!--.*?--&gt;)", RegexOptions.Compiled | RegexOptions.Singleline);
            _newLineRegex = new Regex("\n", RegexOptions.Compiled | RegexOptions.Singleline);
            _tabRegex = new Regex("\t", RegexOptions.Compiled | RegexOptions.Singleline);
            _spaceRegex = new Regex(" +", RegexOptions.Compiled | RegexOptions.Singleline);
            _smileyRegex = new Regex("{SMILIES_PATH}", RegexOptions.Compiled | RegexOptions.Singleline);
            _htmlRegex = new Regex(@"<((?=!\-\-)!\-\-[\s\S]*\-\-|((?=\?)\?[\s\S]*\?|((?=\/)\/[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*|[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*(?:\s[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*(?:=(?:""[^""]*""|'[^']*'|[^'""<\s]*))?)*)\s?\/?))>", RegexOptions.Compiled);

            var bbcodes = _context.PhpbbBbcodes.AsNoTracking().Select(c => new BBTag(c.BbcodeTag, c.BbcodeTpl, string.Empty, false, false)).ToList();

            bbcodes.AddRange(new[]
            {
                    new BBTag("b", "<b>", "</b>"),
                    new BBTag("i", "<span style=\"font-style:italic;\">", "</span>"),
                    new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>"),
                    new BBTag("code", "<span class=\"CodeBlock\">", "</span>"),
                    new BBTag("img", "<br/><img src=\"${content}\" /><br/>", string.Empty, false, false),
                    new BBTag("quote", "<blockquote class=\"PostQuote\">${name}", "</blockquote>",
                        new BBAttribute("name", "", (a) => string.IsNullOrWhiteSpace(a.AttributeValue) ? "" : $"<b>{HttpUtility.HtmlDecode(a.AttributeValue).Trim('"')}</b> a scris:<br/>")) { GreedyAttributeProcessing = true },
                    new BBTag("*", "<li>", "</li>", true, false),
                    new BBTag("list", "<${attr}>", "</${attr}>", true, true,
                        new BBAttribute("attr", "", a => string.IsNullOrWhiteSpace(a.AttributeValue) ? "ul" : $"ol type=\"{a.AttributeValue}\"")),
                    new BBTag("url", "<a href=\"${href}\" target=\"_blank\">", "</a>",
                        new BBAttribute("href", "", a => string.IsNullOrWhiteSpace(a?.AttributeValue) ? "${content}" : a.AttributeValue)),
                    new BBTag("color", "<span style=\"color:${code}\">", "</span>",
                        new BBAttribute("code", "")),
                    new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>",
                        new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")),
                    new BBTag("attachment", "#{AttachmentFileName=${content}/AttachmentIndex=${num}}#", "", false, true,
                        new BBAttribute("num", ""))
                });
            _parser = new BBCodeParser(bbcodes);
        }

        public async Task ProcessPosts(IEnumerable<PostDisplay> Posts, PageContext pageContext, HttpContext httpContext, bool renderAttachments, string toHighlight = null)
        {
            var inlineAttachmentsPosts = new ConcurrentBag<(int PostId, int AttachIndex, _AttachmentPartialModel Attach)>();
            var attachRegex = new Regex("#{AttachmentFileName=[^/]+/AttachmentIndex=[0-9]+}#", RegexOptions.Compiled);
            var highlightWords = SplitHighlightWords(toHighlight);
            var bannedWords = (await _writingService.GetBannedWords()).GroupBy(p => p.Word).Select(grp => grp.FirstOrDefault()).ToDictionary(x => x.Word, y => y.Replacement);
            var mutex = new Mutex();

            Parallel.ForEach(Posts, (p, state1) =>
            {
                p.PostSubject = CensorWords(HttpUtility.HtmlDecode(p.PostSubject), bannedWords);
                p.PostText = CensorWords(p.PostText, bannedWords);
                p.PostSubject = HighlightWords(p.PostSubject, highlightWords);
                p.PostText = HighlightWords(BbCodeToHtml(p.PostText, p.BbcodeUid), highlightWords);

                if (renderAttachments)
                {
                    var matches = from m in attachRegex.Matches(p.PostText).AsEnumerable()
                                  where m.Success
                                  orderby m.Index descending
                                  let parts = m.Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                  let fn = parts[0].Trim("#{".ToCharArray()).Replace("AttachmentFileName=", string.Empty)
                                  let i = int.Parse(parts[1].Trim("}#".ToCharArray()).Replace("AttachmentIndex=", string.Empty))
                                  select (FileName: fn, AttachIndex: i, Original: m);

                    Parallel.ForEach(matches, async (m, state2) =>
                    {
                        _AttachmentPartialModel model = null;
                        int index = m.AttachIndex;
                        var candidates = p.Attachments.Where(a => a.DisplayName == m.FileName).ToList();
                        if (candidates.Count == 1)
                        {
                            model = candidates.First();
                        }
                        else if (candidates.Count > 1)
                        {
                            model = candidates.FirstOrDefault(a => a.DisplayName == m.FileName && candidates.IndexOf(a) == index);
                            if (model == null)
                            {
                                index = candidates.Count - m.AttachIndex - 1;
                                model = candidates.FirstOrDefault(a => candidates.IndexOf(a) == index);
                            }
                        }

                        if (model != null)
                        {
                            try
                            {
                                mutex.WaitOne();

                                p.PostText = p.PostText.Replace(
                                    $"#{{AttachmentFileName={model.DisplayName}/AttachmentIndex={index}}}#",
                                    await _utils.RenderRazorViewToString("_AttachmentPartial", model, pageContext, httpContext)
                                );
                                p.Attachments.Remove(model);
                            }
                            finally
                            {
                                mutex.ReleaseMutex();
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

        public string BbCodeToHtml(string bbCodeText, string bbCodeUid)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            bbCodeText = _parser.ToHtml(bbCodeText, bbCodeUid);
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
                (haystack, needle, startIndex) => (haystack.IndexOf(needle, startIndex, StringComparison.InvariantCultureIgnoreCase), needle),
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
                        if (index == -1)
                        {
                            continue;
                        }

                        var match = input.Substring(index, cleanedMatch.Length);

                        if (!match.RemoveDiacritics().Equals(cleanedMatch, StringComparison.InvariantCultureIgnoreCase))
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
                                if (result.RemoveDiacritics().Equals(cleanedResult, StringComparison.InvariantCultureIgnoreCase) && nextIndex == cleanednextIndex)
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
