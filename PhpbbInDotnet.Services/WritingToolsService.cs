using Dapper;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
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
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    class WritingToolsService : IWritingToolsService
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IStorageService _storageService;
        private readonly IConfiguration _config;
        private readonly IAppCache _cache;
        private readonly ILogger _logger;
        private readonly ITranslationProvider _translationProvider;

        private static readonly Regex EMOJI_REGEX = new(@"^(\:|\;){1}[a-zA-Z0-9\-\)\(\]\[\}\{\\\|\*\'\>\<\?\!]+\:?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WHITESPACE_REGEX = new(@"\s+", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private List<PhpbbSmilies>? _smilies;

        public WritingToolsService(ISqlExecuter sqlExecuter, IStorageService storageService, ITranslationProvider translationProvider,
            IConfiguration config, IAppCache cache, ILogger logger)
        {
            _translationProvider = translationProvider;
            _sqlExecuter = sqlExecuter;
            _storageService = storageService;
            _config = config;
            _cache = cache;
            _logger = logger;
        }

        #region Banned words

        public async Task<List<PhpbbWords>> GetBannedWordsAsync()
            => (await _sqlExecuter.QueryAsync<PhpbbWords>("SELECT * FROM phpbb_words")).AsList();

        public async Task<(string Message, bool? IsSuccess)> ManageBannedWords(List<PhpbbWords> words, List<int> indexesToRemove)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                await _sqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_words (word, replacement) VALUES (@word, @replacement)",
                    words.Where(w => w.WordId == 0));
                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_words SET word = @word, replacement = @replacement WHERE word_id = @wordId",
                    words.Where(w => w.WordId != 0));
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

                await _sqlExecuter.ExecuteAsync(
                    @"INSERT INTO phpbb_bbcodes (bbcode_id, bbcode_tag, bbcode_helpline, display_on_posting, bbcode_match, bbcode_tpl, first_pass_match, first_pass_replace, second_pass_match, second_pass_replace)
                      VALUES (@BbcodeTag, @BbcodeHelpline, @DisplayOnPosting, @BbcodeMatch, @BbcodeTpl, @FirstPassMatch, @FirstPassReplace, @SecondPassMatch, @SecondPassReplace);",
                    codes.Where(c => c.BbcodeId == 0));
                await _sqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_bbcodes
                         SET bbcode_tag  = @BbcodeTag
                            ,bbcode_helpline  = @BbcodeHelpline
                            ,display_on_posting  = @DisplayOnPosting
                            ,bbcode_match  = @BbcodeMatch
                            ,bbcode_tpl  = @BbcodeTpl
                            ,first_pass_match  = @FirstPassMatch
                            ,first_pass_replace  = @FirstPassReplace
                            ,second_pass_match  = @SecondPassMatch
                            ,second_pass_replace = @SecondPassReplace
                       WHERE bbcode_id  = @BbcodeId;",
                    codes.Where(c => c.BbcodeId != 0));
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

        public async Task<string> PrepareTextForSaving(string? text, ITransactionalSqlExecuter? transaction = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            foreach (var sr in await GetLazySmilies(transaction))
            {
                var regex = new Regex(@$"(?<=(^|\s)){Regex.Escape(sr.Code)}(?=($|\s))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);
                var replacement = $"<!-- s{sr.Code} --><img src=\"{_storageService.GetEmojiRelativeUrl(sr.SmileyUrl)}\" alt=\"{sr.Code}\" title=\"{sr.Emotion}\" /><!-- s{sr.Code} -->";
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
            var retention = _config.GetObjectOrDefault<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);
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

            var (Succeeded, Failed) = await _storageService.BulkDeleteAttachments(files.Select(f => f.PhysicalFilename));

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

        public async Task<List<PhpbbSmilies>> GetLazySmilies(ITransactionalSqlExecuter? transaction = null)
            => _smilies ??= await GetSmilies(transaction);

        public async Task<List<PhpbbSmilies>> GetSmilies(ITransactionalSqlExecuter? transaction = null)
        {
            var sql = "SELECT * FROM phpbb_smilies ORDER BY smiley_order";
            if (transaction is not null)
            {
                return (await transaction.QueryAsync<PhpbbSmilies>(sql)).AsList();
            }
            else
            {
				return (await _sqlExecuter.QueryAsync<PhpbbSmilies>(sql)).AsList();
			}
        }

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

                var maxOrder = await _sqlExecuter.ExecuteScalarAsync<int>("SELECT MAX(smiley_order) FROM phpbb_smilies");

                Dictionary<string, int>? orders = null;
                HashSet<int>? codesToDeleteHashSet = null;
                HashSet<string>? smileyGroupsToDeleteHashSet = null;
                await Task.WhenAll(
                    Task.Run(() =>
                    {
                        var counts = dto.Where(d => (!string.IsNullOrWhiteSpace(d.Url) || !string.IsNullOrWhiteSpace(d.File?.FileName)) && !smileyGroupsToDelete.Contains(d.Url!))
                                        .ToDictionary(k => (k.Url ?? k.File?.FileName)!, v => v.Codes!.Count(c => !codesToDelete.Contains(c.Id)));
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

                if (errors.Count == 0)
                {
                    if (flatSource.Count != 0)
                    {
                        await _sqlExecuter.ExecuteAsync(
                            @"INSERT INTO phpbb_smilies (code, emotion, smiley_url, smiley_width, smiley_height, smiley_order, display_on_posting) 
                              VALUES (@Code, @Emotion, @SmileyUrl, @SmileyWidth, @SmileyHeight, @SmileyOrder, @DisplayOnPosting)",
                            flatSource.Where(s => s.SmileyId == 0));
                        await _sqlExecuter.ExecuteAsync(
                            @"UPDATE phpbb_smilies
                                 SET code = @Code
                                    ,emotion = @Emotion
                                    ,smiley_url = @SmileyUrl
                                    ,smiley_width = @SmileyWidth
                                    ,smiley_height = @SmileyHeight
                                    ,smiley_order = @SmileyOrder
                                    ,display_on_posting = @DisplayOnPosting
                               WHERE smiley_id = @SmileyId",
                            flatSource.Where(s => s.SmileyId != 0));
                    }

                    await _sqlExecuter.ExecuteAsync(
                        "DELETE FROM phpbb_smilies WHERE smiley_id IN @ids",
                        new { ids = codesToDelete.DefaultIfEmpty() });

                    foreach (var smileyUrl in smileyGroupsToDelete)
                    {
                        await _sqlExecuter.ExecuteAsync(
                            "DELETE FROM phpbb_smilies WHERE smiley_url = @smileyUrl",
                            new { smileyUrl });
                        if (!await _storageService.DeleteEmoji(smileyUrl))
                        {
                            errors.Add(string.Format(_translationProvider.Admin[language, "EMOJI_NOT_DELETED_FORMAT"], smileyUrl));
                        }
                    }
                }
                else
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
