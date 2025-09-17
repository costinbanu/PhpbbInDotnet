using CodeKicker.BBCode.Core;
using Dapper;
using Diacritics.Extensions;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    partial class BBCodeRenderingService : IBBCodeRenderingService
    {
        private static readonly Regex _htmlRegex = new("<.+?>", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);
        private static readonly Regex _spaceRegex = new(" +", RegexOptions.Compiled | RegexOptions.Singleline, Constants.REGEX_TIMEOUT);
        private static readonly Regex _attachRegex = new("#{AttachmentFileName=[^/]+/AttachmentIndex=[0-9]+}#", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);
        private static readonly Regex _quotedAttachRegex = new("#{QuotedAttachmentFileName=[^/]+/QuotedAttachmentIndexAndPostId=[0-9]+,[0-9]+}#", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);
        private static readonly Regex _quoteAttributeRegex = new("(\".+\")[, ]{0,2}([0-9]+)?", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);
        private static readonly Regex _attrRegex = new(@"\$\{[a-z0-9]+\}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ISqlExecuter _sqlExecuter;
        private readonly IWritingToolsService _writingService;
        private readonly IAppCache _cache;
        private readonly ITranslationProvider _translationProvider;
        private readonly ILogger _logger;
        private readonly IRazorViewService _razorViewService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private delegate (int index, string match) FirstIndexOf(string haystack, string needle, int startIndex);
        private delegate (string result, int endIndex) Transform(string haystack, string needle, int startIndex);

        public BBCodeRenderingService(ISqlExecuter sqlExecuter, IWritingToolsService writingService, IAppCache cache, IHttpContextAccessor httpContextAccessor,
            ITranslationProvider translationProvider, ILogger logger, IRazorViewService razorViewService)
        {
            _sqlExecuter = sqlExecuter;
            _writingService = writingService;
            _cache = cache;
            _translationProvider = translationProvider;
            _logger = logger;
            _razorViewService = razorViewService;
            _httpContextAccessor = httpContextAccessor;
        }

        private Dictionary<string, string>? _bannedWords;
        private async Task<Dictionary<string, string>> GetBannedWords()
        {
            _bannedWords ??= (await _writingService.GetBannedWordsAsync())
                    .GroupBy(p => p.Word)
                    .Select(grp => grp.FirstOrDefault())
                    .ToDictionary(x => x!.Word, y => y!.Replacement);
            return _bannedWords;
        }

        public async Task ProcessPost(PostDto post, bool isPreview, List<string>? toHighlight = null)
        {
            var bannedWords = await GetBannedWords();
            post.PostSubject = HighlightWords(CensorWords(HttpUtility.HtmlDecode(post.PostSubject), bannedWords), toHighlight ?? []);
            post.PostText = HighlightWords(CensorWords(await BbCodeToHtml(post.PostText, post.BbcodeUid), bannedWords), toHighlight ?? []);

            await ProcessAttachments(post);
            await ProcessQuotedAttachments(post, isPreview, toHighlight);
        }

        private async Task ProcessAttachments(PostDto post)
        {
            var matches = from m in _attachRegex.Matches(post.PostText!).AsEnumerable()
                          where m.Success
                          orderby m.Index descending
                          let parts = m.Value.Split(['/'], StringSplitOptions.RemoveEmptyEntries)
                          let fn = parts[0].Trim("#{".ToCharArray()).Replace("AttachmentFileName=", string.Empty)
                          let i = int.Parse(parts[1].Trim("}#".ToCharArray()).Replace("AttachmentIndex=", string.Empty))
                          select (FileName: fn, AttachIndex: i);

            foreach (var (FileName, AttachIndex) in matches)
            {
                var model = await GetAttachmentByNameAndIndex(post.Attachments, FileName, AttachIndex);
                if (model != null)
                {
                    post.PostText = post.PostText!.Replace(
                        $"#{{AttachmentFileName={FileName}/AttachmentIndex={AttachIndex}}}#",
                        await _razorViewService.RenderRazorViewToString("_AttachmentPartial", model));
                    post.Attachments?.Remove(model);
                }
            }

            post.PostText = _attachRegex.Replace(post.PostText!, string.Empty);
        }

        private async Task ProcessQuotedAttachments(PostDto post, bool isPreview, List<string>? toHighlight)
        {
            var matches = from m in _quotedAttachRegex.Matches(post.PostText!).AsEnumerable()
                          where m.Success
                          orderby m.Index descending
                          let parts = m.Value.Split(['/'], StringSplitOptions.RemoveEmptyEntries)
                          let fn = parts[0].Trim("#{".ToCharArray()).Replace("QuotedAttachmentFileName=", string.Empty)
                          let indexAndPostId = parts[1].Trim("}#".ToCharArray()).Replace("QuotedAttachmentIndexAndPostId=", string.Empty).Split([','], StringSplitOptions.RemoveEmptyEntries)
                          let index = int.Parse(indexAndPostId[0])
                          let postId = int.Parse(indexAndPostId[1])
                          select (FileName: fn, AttachIndex: index, PostId: postId);

            var attachmentsByPostId = new Dictionary<int, IEnumerable<AttachmentDto>>();
            foreach (var (FileName, AttachIndex, PostId) in matches)
            {
                if (!attachmentsByPostId.TryGetValue(PostId, out var attachments))
                {
                    var dbAttachments = await _sqlExecuter.QueryAsync<PhpbbAttachments>(
                        "SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY order_in_post",
                        new { PostId });
                    if (dbAttachments.AsList().Count > 0)
                    {
                        attachments = dbAttachments.Select(a => new AttachmentDto(a, post.ForumId, isPreview, _translationProvider.GetLanguage(), PostId, deletedFile: false, toHighlight));
                        attachmentsByPostId.Add(PostId, attachments);
                    }
                    else
                    {
                        attachments = null;
                    }
                }

                var model = await GetAttachmentByNameAndIndex(attachments, FileName, AttachIndex);
                if (model != null)
                {
                    post.PostText = post.PostText!.Replace(
                        $"#{{QuotedAttachmentFileName={FileName}/QuotedAttachmentIndexAndPostId={AttachIndex},{PostId}}}#",
                        await _razorViewService.RenderRazorViewToString("_AttachmentPartial", model));
                }
            }

            post.PostText = _quotedAttachRegex.Replace(post.PostText!, string.Empty);
        }

        private async Task<AttachmentDto?> GetAttachmentByNameAndIndex(IEnumerable<AttachmentDto>? attachments, string fileName, int index)
        {
            var candidates = new List<AttachmentDto>();
            foreach (var (attach, i) in attachments.EmptyIfNull().Indexed())
            {
                var name = await BbCodeToHtml(attach.DisplayName, string.Empty);
                if (name == fileName)
                {
                    candidates.Add(attach);
                }
            }
            if (candidates?.Count == 1)
            {
                return candidates.First();
            }
            else if (candidates?.Count > 1)
            {
                return candidates.FirstOrDefault(a => attachments?.ElementAtOrDefault(index)?.Id == a.Id);
            }
            return null;
        }
        public async Task<string> BbCodeToHtml(string? bbCodeText, string? bbCodeUid)
        {
            if (string.IsNullOrWhiteSpace(bbCodeText))
            {
                return string.Empty;
            }

            string html = bbCodeText;
            try
            {
                var parser = await GetParser();
                html = parser.ToHtml(bbCodeText, bbCodeUid ?? string.Empty);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(bbCodeUid))
                {
                    html = html.Replace($":{bbCodeUid}", string.Empty);
                }
                _logger.Error(ex, "Error parsing bbcode text '{text}' at URL {url}", bbCodeText, _httpContextAccessor.HttpContext?.Request.GetDisplayUrl());
            }
            bbCodeText = HttpUtility.HtmlDecode(html);
            bbCodeText = StringUtility.HtmlCommentRegex.Replace(bbCodeText, string.Empty);
            bbCodeText = bbCodeText.Replace("\t", StringUtility.HtmlSafeWhitespace(4));

            var offset = 0;
            foreach (Match m in _spaceRegex.Matches(bbCodeText))
            {
                var (result, curOffset) = TextHelper.ReplaceAtIndex(bbCodeText, m.Value, StringUtility.HtmlSafeWhitespace(m.Length), m.Index + offset);
                bbCodeText = result;
                offset += curOffset;
            }

            return bbCodeText;
        }

        public List<string> SplitHighlightWords(string? search)
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

        public string HighlightWords(string text, List<string> words)
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

        private string CensorWords(string? text, Dictionary<string, string> wordMap)
        {
            static Regex getRegex(string wildcard)
                => new(@"\b" + Regex.Escape(wildcard).Replace(@"\*", @"\w*").Replace(@"\?", @"\w") + @"\b", RegexOptions.None, Constants.REGEX_TIMEOUT);

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

        private string ProcessAllWords(string? input, IEnumerable<string> words, FirstIndexOf indexOf, Transform transform)
        {
            var cleanedInput = string.Empty;
            var shouldProcess = !string.IsNullOrWhiteSpace(input) && words.Any();

            if (shouldProcess)
            {
                input = StringUtility.ReplaceHtmlDiacritics(input!);
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

                        var match = input!.Substring(index, cleanedMatch.Length);

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

            return input!;
        }

        private static int GetIgnoreOffset(int index, params Match[] matches)
        {
            var match = matches?.Where(m => m.Success && index >= m.Index && index < m.Index + m.Length)?.OrderBy(m => m.Length)?.LastOrDefault();
            if (match != null)
            {
                return match.Index + match.Length;
            }
            return 0;
        }

        string UrlTransformer(string? url)
        {
            url ??= string.Empty;
            try
            {
                if (!url.StartsWith("www", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentException("Bad URL formatting");
                }
                else if (url.StartsWith("www", StringComparison.InvariantCultureIgnoreCase))
                {
                    url = $"http://{url}";
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error at {url}", _httpContextAccessor.HttpContext?.Request.GetDisplayUrl());
            }
            return url;
        }

        string FileNameTransformer(string fileName)
        {
            try
            {
                var newFileName = StringUtility.HtmlCommentRegex.Replace(HttpUtility.HtmlDecode(fileName), string.Empty);
                var result = new StringBuilder();
                for (var i = 0; i < newFileName.Length; i++)
                {
                    result.Append(IllegalChars.Contains(newFileName[i]) ? '-' : newFileName[i]);
                }
                return result.ToString();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error at {url}", _httpContextAccessor.HttpContext?.Request.GetDisplayUrl());
                return fileName;
            }
        }

        static readonly HashSet<char> IllegalChars = new(Path.GetInvalidFileNameChars().Union(Path.GetInvalidPathChars()));
    }
}