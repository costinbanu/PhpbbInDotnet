using CodeKicker.BBCode.Core;
using Dapper;
using Diacritics.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
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
    public class BBCodeRenderingService
    {
        private static readonly Regex _htmlRegex = new Regex(@"<((?=!\-\-)!\-\-[\s\S]*\-\-|((?=\?)\?[\s\S]*\?|((?=\/)\/[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*|[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*(?:\s[^.\-\d][^\/\]'""[!#$%&()*+,;<=>?@^`{|}~ ]*(?:=(?:""[^""]*""|'[^']*'|[^'""<\s]*))?)*)\s?\/?))>", RegexOptions.Compiled);
        private static readonly Regex _spaceRegex = new Regex(" +", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly CommonUtils _utils;
        private readonly ForumDbContext _context;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeParser _parser;
        private readonly Lazy<Dictionary<string, string>> _bannedWords;

        private delegate (int index, string match) FirstIndexOf(string haystack, string needle, int startIndex);
        private delegate (string result, int endIndex) Transform(string haystack, string needle, int startIndex);

        public Dictionary<string, BBTagSummary> TagMap { get; }

        public BBCodeRenderingService(CommonUtils utils, ForumDbContext context, WritingToolsService writingService, BBTagFactory bbTagFactory)
        {
            _utils = utils;
            _context = context;
            _writingService = writingService;
            _bannedWords = new Lazy<Dictionary<string, string>>(() => _writingService.GetBannedWords().GroupBy(p => p.Word).Select(grp => grp.FirstOrDefault()).ToDictionary(x => x.Word, y => y.Replacement));
            var connection = _context.Database.GetDbConnection();
            connection.OpenIfNeeded();

            //we override these temporarily: 18 = link, 13 = youtube
            //we override this for good: 17 = strike (TODO: remove from DB after migration)
            var tagList = connection.Query<PhpbbBbcodes>("SELECT * FROM phpbb_bbcodes WHERE bbcode_id NOT IN (18, 13, 17)").AsList();

            var (bbTags, tagMap) = bbTagFactory.GenerateCompleteTagListAndMap(tagList);
            _parser = new BBCodeParser(bbTags);
            TagMap = tagMap;
        }

        public async Task ProcessPost(PostDto post, PageContext pageContext, HttpContext httpContext, bool renderAttachments, string toHighlight = null)
        {
            var attachRegex = new Regex("#{AttachmentFileName=[^/]+/AttachmentIndex=[0-9]+}#", RegexOptions.Compiled);
            var highlightWords = SplitHighlightWords(toHighlight);

            post.PostSubject = CensorWords(HttpUtility.HtmlDecode(post.PostSubject), _bannedWords.Value);
            post.PostSubject = HighlightWords(post.PostSubject, highlightWords);
            post.PostText = HighlightWords(BbCodeToHtml(post.PostText, post.BbcodeUid), highlightWords);

            if (renderAttachments)
            {
                var matches = from m in attachRegex.Matches(post.PostText).AsEnumerable()
                              where m.Success
                              orderby m.Index descending
                              let parts = m.Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                              let fn = parts[0].Trim("#{".ToCharArray()).Replace("AttachmentFileName=", string.Empty)
                              let i = int.Parse(parts[1].Trim("}#".ToCharArray()).Replace("AttachmentIndex=", string.Empty))
                              select (FileName: fn, AttachIndex: i, Original: m);

                foreach (var (FileName, AttachIndex, Original) in matches)
                {
                    AttachmentDto model = null;
                    int index = AttachIndex;
                    var candidates = post.Attachments.Where(a => a.DisplayName == FileName).ToList();
                    if (candidates.Count == 1)
                    {
                        model = candidates.First();
                    }
                    else if (candidates.Count > 1)
                    {
                        model = candidates.FirstOrDefault(a => a.DisplayName == FileName && candidates.IndexOf(a) == index);
                        if (model == null)
                        {
                            index = candidates.Count - AttachIndex - 1;
                            model = candidates.FirstOrDefault(a => candidates.IndexOf(a) == index);
                        }
                    }

                    if (model != null)
                    {
                        post.PostText = post.PostText.Replace(
                            $"#{{AttachmentFileName={model.DisplayName}/AttachmentIndex={index}}}#",
                            await _utils.RenderRazorViewToString("_AttachmentPartial", model, pageContext, httpContext)
                        );
                        post.Attachments.Remove(model);
                    }
                }
            }
            else
            {
                post.PostText = attachRegex.Replace(post.PostText, string.Empty);
            }

            post.PostText = attachRegex.Replace(post.PostText, string.Empty);
        }

        public string BbCodeToHtml(string bbCodeText, string bbCodeUid)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            bbCodeText = CensorWords(bbCodeText, _bannedWords.Value);
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
                _utils.HandleError(ex, $"Error parsing bbcode text '{bbCodeText}'");
            }
            bbCodeText = HttpUtility.HtmlDecode(html);
            bbCodeText = _utils.HtmlCommentRegex.Replace(bbCodeText, string.Empty);
            bbCodeText = bbCodeText.Replace("{SMILIES_PATH}", Constants.SMILEY_PATH);
            bbCodeText = bbCodeText.Replace("\t", _utils.HtmlSafeWhitespace(4));

            var offset = 0;
            foreach (Match m in _spaceRegex.Matches(bbCodeText))
            {
                var (result, curOffset) = TextHelper.ReplaceAtIndex(bbCodeText, m.Value, _utils.HtmlSafeWhitespace(m.Length), m.Index + offset);
                bbCodeText = result;
                offset += curOffset;
            }

            return bbCodeText;
        }

        public (string text, string uid, string bitfield) TransformForBackwardsCompatibility(string bbCodeText)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return (string.Empty, string.Empty, string.Empty);
            }
            return _parser.TransformForBackwardsCompatibility(bbCodeText);
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
                => new Regex(@"\b" + Regex.Escape(wildcard).Replace(@"\*", @"\w*").Replace(@"\?", @"\w") + @"\b", RegexOptions.None, TimeSpan.FromSeconds(20));

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
                    var (result, offset) = TextHelper.ReplaceAtIndex(haystack, needle, replacement, index);
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

                        var oldValue = index;
                        while (index != -1)
                        {
                            var startIndex = 0;
                            var tag = htmlTagsLocation.FirstOrDefault(x => index >= x.Position && index < x.Position + x.Length);
                            if (tag == default)
                            {
                                var (cleanedResult, cleanednextIndex) = transform(cleanedInput, cleanedMatch, index);
                                input = cleanedResult;
                                cleanedInput = cleanedResult;
                                startIndex = cleanednextIndex;
                            }
                            else
                            {
                                startIndex = tag.Position + tag.Length;
                            }
                            index = startIndex > cleanedInput.Length ? -1 : indexOf(cleanedInput, cleanedWord, startIndex).index;
                            if (index == oldValue)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return input;
        }
    }
}
