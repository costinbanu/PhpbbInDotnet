using CodeKicker.BBCode.Core;
using Dapper;
using Diacritics.Extensions;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    public class BBCodeRenderingService : MultilingualServiceBase
    {
        private static readonly Regex _htmlRegex = new Regex("<.+?>", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);
        private static readonly Regex _spaceRegex = new Regex(" +", RegexOptions.Compiled | RegexOptions.Singleline, Constants.REGEX_TIMEOUT);
        private static readonly Regex _attachRegex = new Regex("#{AttachmentFileName=[^/]+/AttachmentIndex=[0-9]+}#", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);

        private readonly ForumDbContext _context;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeParser _parser;
        private readonly Lazy<Dictionary<string, string>> _bannedWords;
        private readonly IAppCache _cache;
        private readonly Regex _attrRegex;

        private delegate (int index, string match) FirstIndexOf(string haystack, string needle, int startIndex);
        private delegate (string result, int endIndex) Transform(string haystack, string needle, int startIndex);

        public Dictionary<string, BBTagSummary> TagMap { get; }

        public BBCodeRenderingService(CommonUtils utils, ForumDbContext context, WritingToolsService writingService, IAppCache cache, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
            : base(utils, languageProvider, httpContextAccessor)
        {
            _context = context;
            _writingService = writingService;
            _bannedWords = new Lazy<Dictionary<string, string>>(() => _writingService.GetBannedWords().GroupBy(p => p.Word).Select(grp => grp.FirstOrDefault()).ToDictionary(x => x.Word, y => y.Replacement));
            _cache = cache;

            var tagList = _context.Database.GetDbConnection().Query<PhpbbBbcodes>("SELECT * FROM phpbb_bbcodes").AsList();

            _attrRegex = new Regex(@"\$\{[a-z0-9]+\}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var (bbTags, tagMap) = GenerateCompleteTagListAndMap(tagList);
            _parser = new BBCodeParser(bbTags);
            TagMap = tagMap;
        }

        public async Task ProcessPost(PostDto post, PageContext pageContext, HttpContext httpContext, bool renderAttachments, string toHighlight = null)
        {
            var highlightWords = SplitHighlightWords(toHighlight);
            post.PostSubject = HighlightWords(CensorWords(HttpUtility.HtmlDecode(post.PostSubject), _bannedWords.Value), highlightWords);
            post.PostText = HighlightWords(CensorWords(BbCodeToHtml(post.PostText, post.BbcodeUid), _bannedWords.Value), highlightWords);

            if (renderAttachments)
            {
                var matches = from m in _attachRegex.Matches(post.PostText).AsEnumerable()
                              where m.Success
                              orderby m.Index descending
                              let parts = m.Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                              let fn = parts[0].Trim("#{".ToCharArray()).Replace("AttachmentFileName=", string.Empty)
                              let i = int.Parse(parts[1].Trim("}#".ToCharArray()).Replace("AttachmentIndex=", string.Empty))
                              select (FileName: fn, AttachIndex: i);

                foreach (var (FileName, AttachIndex) in matches)
                {
                    AttachmentDto model = null;
                    var candidates = post.Attachments.Where(a => BbCodeToHtml(a.DisplayName, string.Empty) == FileName).ToList();
                    if (candidates.Count == 1)
                    {
                        model = candidates.First();
                    }
                    else if (candidates.Count > 1)
                    {
                        model = candidates.FirstOrDefault(a => post.Attachments.ElementAtOrDefault(AttachIndex)?.Id == a.Id);
                    }

                    if (model != null)
                    {
                        post.PostText = post.PostText.Replace(
                            $"#{{AttachmentFileName={FileName}/AttachmentIndex={AttachIndex}}}#",
                            await Utils.RenderRazorViewToString("_AttachmentPartial", model, pageContext, httpContext)
                        );
                        post.Attachments.Remove(model);
                    }
                }
            }

            post.PostText = _attachRegex.Replace(post.PostText, string.Empty);
        }

        public string BbCodeToHtml(string bbCodeText, string bbCodeUid)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            string html = bbCodeText;
            try
            {
                html = _parser.ToHtml(bbCodeText, bbCodeUid ?? string.Empty);
            }
            catch(Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(bbCodeUid))
                {
                    html = html.Replace($":{bbCodeUid}", string.Empty);
                }
                Utils.HandleError(ex, $"Error parsing bbcode text '{bbCodeText}'");
            }
            bbCodeText = HttpUtility.HtmlDecode(html);
            bbCodeText = Utils.HtmlCommentRegex.Replace(bbCodeText, string.Empty);
            bbCodeText = bbCodeText.Replace("{SMILIES_PATH}", Constants.SMILEY_PATH);
            bbCodeText = bbCodeText.Replace("\t", Utils.HtmlSafeWhitespace(4));

            var offset = 0;
            foreach (Match m in _spaceRegex.Matches(bbCodeText))
            {
                var (result, curOffset) = TextHelper.ReplaceAtIndex(bbCodeText, m.Value, Utils.HtmlSafeWhitespace(m.Length), m.Index + offset);
                bbCodeText = result;
                offset += curOffset;
            }

            return bbCodeText;
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
                input: text,
                words: words,
                indexOf: (haystack, needle, startIndex) => (haystack.IndexOf(needle, startIndex, StringComparison.InvariantCultureIgnoreCase), needle),
                transform: (haystack, needle, index) =>
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
                => new Regex(@"\b" + Regex.Escape(wildcard).Replace(@"\*", @"\w*").Replace(@"\?", @"\w") + @"\b", RegexOptions.None, Constants.REGEX_TIMEOUT);

            return ProcessAllWords(
                input: text,
                words: wordMap.Keys,
                indexOf: (haystack, needle, startIndex) =>
                {
                    var match = getRegex(needle).Match(haystack, startIndex);
                    return match.Success ? (match.Index, match.Value) : (-1, needle);
                },
                transform: (haystack, needle, index) =>
                {
                    var replacement = wordMap.Select(x => KeyValuePair.Create(getRegex(x.Key), x.Value)).FirstOrDefault(x => x.Key.IsMatch(needle)).Value;
                    if (replacement == null)
                    {
                        return (haystack, index + needle.Length);
                    }
                    var (result, offset) = TextHelper.ReplaceAtIndex(haystack, needle, replacement, index);
                    return (result, index + replacement.Length);
                }
            );
        }

        private string ProcessAllWords(string input, IEnumerable<string> words, FirstIndexOf indexOf, Transform transform)
        {
            var cleanedInput = string.Empty;
            var shouldProcess = !string.IsNullOrWhiteSpace(input) && words.Any();

            if (shouldProcess)
            {
                input = input.ReplaceHtmlDiacritics();
                cleanedInput = input.RemoveDiacritics();
                shouldProcess = cleanedInput.Length == input.Length;
            }

            if (shouldProcess)
            {
                foreach (var word in words)
                {
                    var cleanedWord = HttpUtility.HtmlDecode(word.RemoveDiacritics());
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

                        var htmlMatch = _htmlRegex.Match(cleanedInput, 0);
                        var attachMatch = _attachRegex.Match(cleanedInput, 0);
                        var start = DateTime.UtcNow;

                        while (true)
                        {
                            var startIndex = 0;
                            var ignoreOffset = GetIgnoreOffset(index, htmlMatch, attachMatch);
                            
                            if (ignoreOffset == 0)
                            {
                                var (transformedResult, transformednextIndex) = transform(input, cleanedMatch, index);
                                input = transformedResult;
                                cleanedInput = transformedResult.RemoveDiacritics();
                                startIndex = transformednextIndex;
                            }
                            else
                            {
                                startIndex = ignoreOffset;
                            }

                            var oldIndex = index;
                            index = startIndex > cleanedInput.Length ? -1 : indexOf(cleanedInput, cleanedWord, startIndex).index;
                            
                            if (index == oldIndex || index == -1 || DateTime.UtcNow.Subtract(start) >= Constants.REGEX_TIMEOUT)
                            {
                                break;
                            }

                            htmlMatch = _htmlRegex.Match(cleanedInput, oldIndex);
                            attachMatch = _attachRegex.Match(cleanedInput, oldIndex);
                        }
                    }
                }
            }

            return input;
        }

        private int GetIgnoreOffset(int index, params Match[] matches)
        {
            var match = matches?.Where(m => m.Success && index >= m.Index && index < m.Index + m.Length)?.OrderBy(m => m.Length)?.LastOrDefault();
            if (match != null)
            {
                return match.Index + match.Length;
            }
            return 0;
        }

        private (List<BBTag> BBTags, Dictionary<string, BBTagSummary> TagMap) GenerateCompleteTagListAndMap(IEnumerable<PhpbbBbcodes> dbCodes)
        {
            var lang = GetLanguage().GetAwaiter().GetResult();
            return _cache.GetOrAdd(
                $"TAGS_MAP_{lang}",
                () =>
                {

                    var tagsCache = new Dictionary<string, (BBTag Tag, BBTagSummary Summary)>
                    {
                        ["b"] = (
                            Tag: new BBTag("b", "<b>", "</b>", 1),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[b]",
                                CloseTag = "[/b]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["i"] = (
                            Tag: new BBTag("i", "<span style=\"font-style:italic;\">", "</span>", 2),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[i]",
                                CloseTag = "[/i]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["u"] = (
                            Tag: new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>", 7),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[u]",
                                CloseTag = "[/u]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["color"] = (
                            Tag: new BBTag("color", "<span style=\"color:${code}\">", "</span>", 6, "", true,
                                new BBAttribute("code", "")),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[color]",
                                CloseTag = "[/color]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = false
                            }
                        ),

                        ["size"] = (
                            Tag: new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>", 5, "", true,
                                new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[size]",
                                CloseTag = "[/size]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["quote"] = (
                            Tag: new BBTag("quote", "<blockquote class=\"PostQuote\">${name}", "</blockquote>", 0, "", true,
                                new BBAttribute("name", "", (a) => string.IsNullOrWhiteSpace(a.AttributeValue) ? "" : string.Format(LanguageProvider.BasicText[lang, "WROTE_FORMAT"], HttpUtility.HtmlDecode(a.AttributeValue).Trim('"')), HtmlEncodingMode.UnsafeDontEncode))
                            {
                                GreedyAttributeProcessing = true
                            },
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[quote]",
                                CloseTag = "[/quote]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["img"] = (
                            Tag: new BBTag("img", "<br/><img src=\"${content}\" onload=\"resizeImage(this)\" style=\"width: 100px; height: 100px\" /><br/>", string.Empty, false, BBTagClosingStyle.RequiresClosingTag, content => UrlTransformer(content), false, 4, allowUrlProcessingAsText: false),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[img]",
                                CloseTag = "[/img]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["url"] = (
                            Tag: new BBTag("url", "<a href=\"${href}\" target=\"_blank\">", "</a>", 3, "", false,
                                new BBAttribute("href", "", context => UrlTransformer(string.IsNullOrWhiteSpace(context?.AttributeValue) ? context.TagContent : context.AttributeValue), HtmlEncodingMode.UnsafeDontEncode)),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[url]",
                                CloseTag = "[/url]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = true
                            }
                        ),

                        ["code"] = (
                            Tag: new BBTag("code", "<span class=\"CodeBlock\">", "</span>", 8, allowUrlProcessingAsText: false),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[code]",
                                CloseTag = "[/code]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = false
                            }
                        ),

                        ["list"] = (
                            Tag: new BBTag("list", "<${attr}>", "</${attr}>", true, true, 9, "", true,
                                new BBAttribute("attr", "", a => string.IsNullOrWhiteSpace(a.AttributeValue) ? "ul style='list-style-type: circle'" : $"ol type=\"{a.AttributeValue}\"")),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[list]",
                                CloseTag = "[/list]",
                                ShowOnPage = true,
                                ShowWhenCollapsed = false
                            }
                        ),

                        ["*"] = (
                            Tag: new BBTag("*", "<li>", "</li>", true, BBTagClosingStyle.AutoCloseElement, x => x, true, 20),
                            Summary: new BBTagSummary
                            {
                                OpenTag = "[*]",
                                CloseTag = "",
                                ShowOnPage = true,
                                ShowWhenCollapsed = false
                            }
                        ),

                        ["attachment"] = (
                            Tag: new BBTag("attachment", "#{AttachmentFileName=${content}/AttachmentIndex=${num}}#", "", false, BBTagClosingStyle.AutoCloseElement, x => Utils.HtmlCommentRegex.Replace(HttpUtility.HtmlDecode(x), string.Empty), 12, "", true,
                                new BBAttribute("num", "")),
                            Summary: new BBTagSummary
                            {
                                ShowOnPage = false
                            }
                        ),
                    };

                    var tags = new List<BBTag>(tagsCache.Count);
                    var tagMap = new Dictionary<string, BBTagSummary>(tagsCache.Count);
                    foreach (var tag in tagsCache)
                    {
                        tags.Add(tag.Value.Tag);
                        if (tag.Value.Summary.ShowOnPage)
                        {
                            tagMap.TryAdd(tag.Key, tag.Value.Summary);
                        }
                    }
                    foreach (var code in dbCodes)
                    {
                        var attributes = _attrRegex.Matches(code.BbcodeTpl).Where(m => m.Success && !m.Value.Equals("${content}", StringComparison.InvariantCultureIgnoreCase)).Select(m => new BBAttribute(m.Value.Trim("${}".ToCharArray()), "", ctx => ctx.AttributeValue)).ToArray();
                        tags.Add(new BBTag(code.BbcodeTag, code.BbcodeTpl, string.Empty, false, false, code.BbcodeId, "", true, attributes));

                        if (code.DisplayOnPosting.ToBool())
                        {
                            tagMap.TryAdd(code.BbcodeTag, new BBTagSummary
                            {
                                OpenTag = $"[{code.BbcodeTag}]",
                                CloseTag = $"[/{code.BbcodeTag}]",
                                ShowOnPage = code.DisplayOnPosting.ToBool(),
                                ShowWhenCollapsed = false
                            });
                        }
                    }
                    return (tags, tagMap);
                },
                DateTimeOffset.UtcNow.AddHours(4)
            );
        }

        private string UrlTransformer(string url)
        {
            if (!url.StartsWith("www", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException("Bad URL formatting");
            }
            else if (url.StartsWith("www", StringComparison.InvariantCultureIgnoreCase))
            {
                url = $"//{url}";
            }
            return url;
        }
    }
}
