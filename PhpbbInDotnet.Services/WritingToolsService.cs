using Dapper;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    class WritingToolsService : IWritingToolsService
    {
        private readonly IForumDbContext _context;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IStorageService _storageService;
        private readonly IConfiguration _config;
        private readonly IAppCache _cache;
        private readonly ILogger _logger;
        private readonly ITranslationProvider _translationProvider;

        private static readonly Regex EMOJI_REGEX = new(@"^(\:|\;){1}[a-zA-Z0-9\-\)\(\]\[\}\{\\\|\*\'\>\<\?\!]+\:?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WHITESPACE_REGEX = new(@"\s+", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private List<PhpbbSmilies>? _smilies;

        public WritingToolsService(IForumDbContext context, ISqlExecuter sqlExecuter, IStorageService storageService, ITranslationProvider translationProvider,
            IConfiguration config, IAppCache cache, ILogger logger)
        {
            _translationProvider = translationProvider;
            _context = context;
            _sqlExecuter = sqlExecuter;
            _storageService = storageService;
            _config = config;
            _cache = cache;
            _logger = logger;
        }

        #region Banned words

        public async Task<List<PhpbbWords>> GetBannedWordsAsync()
            => (await _sqlExecuter.QueryAsync<PhpbbWords>("SELECT * FROM phpbb_words")).AsList();

        public List<PhpbbWords> GetBannedWords()
            => _sqlExecuter.Query<PhpbbWords>("SELECT * FROM phpbb_words").AsList();

        public async Task<(string Message, bool? IsSuccess)> ManageBannedWords(List<PhpbbWords> words, List<int> indexesToRemove)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                await _context.PhpbbWords.AddRangeAsync(words.Where(w => w.WordId == 0));
                _context.PhpbbWords.UpdateRange(words.Where(w => w.WordId != 0));
                await _context.SaveChangesAsync();

                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_words WHERE word_id IN @ids",
                    new { ids = indexesToRemove.Select(i => words[i].WordId).DefaultIfEmpty() });

                return (_translationProvider.Admin[language, "BANNED_WORDS_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ManageBannedWords failed");
                return (_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        #endregion Banned words

        #region Text & BB Codes

        public string GetBbCodesCacheKey(string language)
            => $"TAGS_MAP_{language}";

        public async Task<(string Message, bool? IsSuccess)> ManageBBCodes(List<PhpbbBbcodes> codes, List<int> indexesToRemove, List<int> indexesToDisplay)
        {
            var currentLanguage = _translationProvider.GetLanguage();
            try
            {
                indexesToDisplay.ForEach(i => codes[i].DisplayOnPosting = 1);
                await _context.PhpbbBbcodes.AddRangeAsync(codes.Where(c => c.BbcodeId == 0));
                _context.PhpbbBbcodes.UpdateRange(codes.Where(c => c.BbcodeId != 0));
                await _context.SaveChangesAsync();

                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_bbcodes WHERE bbcode_id IN @ids",
                    new { ids = indexesToRemove.Select(i => codes[i].BbcodeId).DefaultIfEmpty() });

                foreach (var language in _translationProvider.AllLanguages)
                {
                    _cache.Remove(GetBbCodesCacheKey(language));
                }

                return (_translationProvider.Admin[currentLanguage, "BBCODES_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ManageBBCodes failed");
                return (_translationProvider.Errors[currentLanguage, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<string> PrepareTextForSaving(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            foreach (var sr in await GetLazySmilies())
            {
                var regex = new Regex(@$"(?<=(^|\s)){Regex.Escape(sr.Code)}(?=($|\s))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);
                var replacement = $"<!-- s{sr.Code} --><img src=\"{_storageService.GetFileUrl(sr.SmileyUrl, FileType.Emoji)}\" alt=\"{sr.Code}\" title=\"{sr.Emotion}\" /><!-- s{sr.Code} -->";
                text = regex.Replace(text, replacement);
            }
            return text;
        }

        public string CleanBbTextForDisplay(string? text, string? uid)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            uid ??= string.Empty;

            var uidRegex = new Regex($":{uid}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);
            var tagRegex = new Regex(@"(:[a-z])(\]|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);
            var cleanTextTemp = string.IsNullOrWhiteSpace(uid) ? HttpUtility.HtmlDecode(text) : uidRegex.Replace(HttpUtility.HtmlDecode(text), string.Empty);
            var noUid = tagRegex.Replace(cleanTextTemp, "$2");

            string replace(string input, string pattern, int groupsIndex)
            {
                var output = input;
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);
                var matches = regex.Matches(output);
                try
                {
                    foreach (Match m in matches)
                    {
                        output = output.Replace(m.Value, m.Groups[groupsIndex].Value);
                    }
                }
                catch { }
                return output;
            }

            var noSmileys = replace(noUid, "<!-- s(:?.+?) -->.+?<!-- s:?.+?:? -->", 1);
            var noLinks = replace(noSmileys, @"<a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1.+?</a>", 2);
            var noComments = replace(noLinks, @"<!-- .*? -->", 1);

            return noComments;
        }

        #endregion Text & BB Codes

        #region Orphan files

        public async Task<(string Message, bool? IsSuccess)> DeleteOrphanedFiles()
        {
            var language = _translationProvider.GetLanguage();
            var retention = _config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);
            var files = await _sqlExecuter.QueryAsync<PhpbbAttachments>(
                "SELECT * FROM phpbb_attachments WHERE is_orphan = 1 AND @now - filetime > @retention",
                new
                {
                    now = DateTime.UtcNow.ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });

            if (!files.Any())
            {
                return (_translationProvider.Admin[language, "NO_ORPHANED_FILES_DELETED"], true);
            }

            var (Succeeded, Failed) = _storageService.BulkDeleteAttachments(files.Select(f => f.PhysicalFilename));

            if (Succeeded?.Any() == true)
            {
                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_attachments WHERE attach_id IN @ids",
                    new { ids = files.Where(f => Succeeded.Contains(f.PhysicalFilename)).Select(f => f.AttachId).DefaultIfEmpty() }
                );
            }

            if (Failed?.Any() == true)
            {
                return (string.Format(_translationProvider.Admin[language, "SOME_ORPHANED_FILES_DELETED_FORMAT"], string.Join(",", Succeeded ?? Enumerable.Empty<string>()), string.Join(",", Failed)), false);
            }
            else
            {
                return (string.Format(_translationProvider.Admin[language, "ORPHANED_FILES_DELETED_FORMAT"], string.Join(",", Succeeded ?? Enumerable.Empty<string>())), true);
            }
        }

        #endregion Orphan files

        #region Smilies

        public async Task<List<PhpbbSmilies>> GetLazySmilies()
            => _smilies ??= await GetSmilies();

        public async Task<List<PhpbbSmilies>> GetSmilies()
            => (await _sqlExecuter.QueryAsync<PhpbbSmilies>("SELECT * FROM phpbb_smilies ORDER BY smiley_order")).AsList();

        public async Task<(string Message, bool? IsSuccess)> ManageSmilies(List<UpsertSmiliesDto> dto, List<string> newOrder, List<int> codesToDelete, List<string> smileyGroupsToDelete)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                if (codesToDelete.Any())
                {
                    await _sqlExecuter.ExecuteAsync("DELETE FROM phpbb_smilies WHERE smiley_id IN @codesToDelete", new { codesToDelete });
                }
                if (smileyGroupsToDelete.Any())
                {
                    await _sqlExecuter.ExecuteAsync("DELETE FROM phpbb_smilies WHERE smiley_url IN @smileyGroupsToDelete", new { smileyGroupsToDelete });
                }

                var maxOrder = await _context.PhpbbSmilies.AsNoTracking().DefaultIfEmpty().MaxAsync(s => s == null ? 0 : s.SmileyOrder);

                Dictionary<string, int>? orders = null;
                HashSet<int>? codesToDeleteHashSet = null;
                HashSet<string>? smileyGroupsToDeleteHashSet = null;
                await Task.WhenAll(
                    Task.Run(() => dto.Where(d => !smileyGroupsToDelete.Contains(d.Url!)).ToDictionary(k => k.Url!, v => v.Codes!.Count(c => !codesToDelete.Contains(c.Id)))).ContinueWith(async countsTask =>
                    {
                        var counts = await countsTask;
                        var increment = 1;
                        orders = new Dictionary<string, int>(newOrder.Count);
                        for (var i = 0; i < newOrder.Count; i++)
                        {
                            orders.Add(newOrder[i], i + increment);
                            increment = counts.TryGetValue(newOrder[i], out var val) ? val : 1;
                        }
                    }),
                    Task.Run(() => codesToDeleteHashSet = new HashSet<int>(codesToDelete)),
                    Task.Run(() => smileyGroupsToDeleteHashSet = new HashSet<string>(smileyGroupsToDelete))
                );
                var errors = new List<string>(dto.Count * 2);
                var flatSource = new List<PhpbbSmilies>(dto.Count * 2);
                var maxSize = _config.GetObject<ImageSize>("EmojiMaxSize");

                foreach (var smiley in dto)
                {
                    if (smileyGroupsToDeleteHashSet?.Contains(smiley.Url!) == true)
                    {
                        continue;
                    }

                    var fileName = smiley.File?.FileName ?? smiley.Url;
                    var validCodes = smiley.Codes?.Where(c => EMOJI_REGEX.IsMatch(c?.Value ?? string.Empty)) ?? Enumerable.Empty<UpsertSmiliesDto.SmileyCode>();

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        errors.Add(string.Format(_translationProvider.Admin[language, "MISSING_EMOJI_FILE_FORMAT"], smiley.Codes?.FirstOrDefault()?.Value));
                        continue;
                    }
                    fileName = WHITESPACE_REGEX.Replace(fileName, "+");

                    if (!validCodes.Any())
                    {
                        errors.Add(string.Format(_translationProvider.Admin[language, "INVALID_EMOJI_CODE_FORMAT"], fileName));
                    }
                    else if (smiley.File != null)
                    {
                        using var stream = smiley.File.OpenReadStream();
                        using var bmp = Image.Load(stream);
                        stream.Seek(0, SeekOrigin.Begin);
                        if (bmp.Width > maxSize.Width || bmp.Height > maxSize.Height)
                        {
                            errors.Add(string.Format(_translationProvider.Admin[language, "INVALID_EMOJI_FILE_FORMAT"], fileName, maxSize.Width, maxSize.Height));
                        }
                        else
                        {
                            if (!await _storageService.UpsertEmoji(fileName, stream))
                            {
                                errors.Add(string.Format(_translationProvider.Admin[language, "EMOJI_NOT_UPLOADED_FORMAT"], fileName));
                            }
                        }
                    }

                    var order = orders is not null && orders.TryGetValue(smiley.Url ?? string.Empty, out var val) ? val : maxOrder++;
                    foreach (var code in validCodes)
                    {
                        if (codesToDeleteHashSet?.Contains(code.Id) == true)
                        {
                            continue;
                        }

                        flatSource.Add(new PhpbbSmilies
                        {
                            SmileyId = code.Id,
                            Code = code.Value!,
                            SmileyUrl = fileName,
                            Emotion = smiley.Emotion ?? string.Empty,
                            SmileyOrder = order
                        });
                    }
                }

                if (flatSource.Count == 0)
                {
                    return (string.Join(Environment.NewLine, errors), false);
                }

                await _context.PhpbbSmilies.AddRangeAsync(flatSource.Where(s => s.SmileyId == 0));
                _context.PhpbbSmilies.UpdateRange(flatSource.Where(s => s.SmileyId != 0));
                await _context.SaveChangesAsync();

                if (errors.Count > 0)
                {
                    _logger.Warning(new AggregateException(errors.Select(e => new Exception(e))), "Error managing emojis");
                    return (string.Join(Environment.NewLine, errors), null);
                }
                return (_translationProvider.Admin[language, "EMOJI_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ManageSmilies failed");
                return (_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        #endregion Smilies

    }
}
