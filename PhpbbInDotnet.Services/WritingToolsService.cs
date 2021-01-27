﻿using Dapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using PhpbbInDotnet.Languages;
using Microsoft.AspNetCore.Http;
using System;

namespace PhpbbInDotnet.Services
{
    public class WritingToolsService : MultilingualServiceBase
    {
        private readonly ForumDbContext _context;
        private readonly StorageService _storageService;

        public WritingToolsService(ForumDbContext context, StorageService storageService, CommonUtils utils, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
            : base(utils, languageProvider, httpContextAccessor)
        {
            _context = context;
            _storageService = storageService;
        }

        public async Task<List<PhpbbWords>> GetBannedWordsAsync()
            => (await _context.Database.GetDbConnection().QueryAsync<PhpbbWords>("SELECT * FROM phpbb_words")).AsList();

        public List<PhpbbWords> GetBannedWords()
            => _context.Database.GetDbConnection().Query<PhpbbWords>("SELECT * FROM phpbb_words").AsList();

        public async Task<(string Message, bool? IsSuccess)> ManageBannedWords(List<PhpbbWords> words, List<int> indexesToRemove)
        {
            var lang = await GetLanguage();
            try
            {
                var newWords = from w2 in words
                               join w in _context.PhpbbWords.AsNoTracking()
                               on w2.WordId equals w.WordId
                               into joined
                               from j in joined.DefaultIfEmpty()
                               where j == null && !string.IsNullOrWhiteSpace(w2.Word) && !string.IsNullOrWhiteSpace(w2.Replacement)
                               select w2;
                
                if (newWords.Any())
                {
                    await _context.PhpbbWords.AddRangeAsync(newWords);
                }

                var toRemove = new List<PhpbbWords>();
                foreach (var idx in indexesToRemove)
                {
                        toRemove.Add(await _context.PhpbbWords.FirstOrDefaultAsync(x => x.WordId == words[idx].WordId));
                }
                _context.PhpbbWords.RemoveRange(toRemove);
                
                var editedWords = from w in await _context.PhpbbWords.ToListAsync()
                                  join w2 in words 
                                  on w.WordId equals w2.WordId
                                  join r in toRemove
                                  on w.WordId equals r.WordId
                                  into joined
                                  from j in joined.DefaultIfEmpty()
                                  where j == default && w.Replacement != w2.Replacement
                                  select (Word: w, w2.Replacement);

                foreach (var (Word, Replacement) in editedWords)
                {
                    Word.Replacement = Replacement;
                }

                await _context.SaveChangesAsync();

                return (LanguageProvider.Admin[lang, "BANNED_WORDS_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex, "ManageBannedWords failed");
                return (LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> ManageBBCodes(List<PhpbbBbcodes> codes, List<int> toRemove, List<int> toDisplay)
        {
            var lang = await GetLanguage();
            try
            { asta nu stiu daca e bine
                codes.ForEach(code =>
                {
                    code.BbcodeTpl ??= string.Empty;
                    code.BbcodeHelpline ??= string.Empty;
                });
                toDisplay.ForEach(i => codes[i].DisplayOnPosting = 1);
                await _context.PhpbbBbcodes.AddRangeAsync(codes.Where(c => c.BbcodeId == 0));
                _context.PhpbbBbcodes.UpdateRange(codes.Where(c => c.BbcodeId != 0));
                await _context.SaveChangesAsync();

                var conn = _context.Database.GetDbConnection();
                //await conn.ExecuteAsync(
                //    "INSERT INTO phpbb_codes (bbcode_tag, bbcode_tpl, bbcode_helpline, display_on_posting) VALUES (@BbcodeTag, @BbcodeTpl, @Display)",
                //    codes.Where(c => c.BbcodeId == 0).Select(c => new { c.BbcodeTag, c.BbcodeTpl, Display = toDisplay.Contains(codes.IndexOf()) }
                //);
                await conn.ExecuteAsync("DELETE FROM phpbb_bbcodes WHERE bbcode_id IN @ids", new { ids = toRemove.Select(i => codes[i].BbcodeId) });
                //await conn.ExecuteAsync("UPDATE phpbb_bbcodes SET display_on_posting = 1 WHERE bbcode_id IN @ids", new { ids = toDisplay.Select(i => codes[i].BbcodeId) });
                return (LanguageProvider.Admin[lang, "BBCODES_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex, "ManageBBCodes failed");
                return (LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<List<AttachmentManagementDto>> GetOrphanedFiles()
        {
            var connection = _context.Database.GetDbConnection();
            return (await connection.QueryAsync<AttachmentManagementDto>("SELECT a.*, u.username FROM phpbb_attachments a JOIN phpbb_users u on a.poster_id = u.user_id WHERE a.is_orphan = 1")).AsList();
        }

        public string GetCacheKey(int currentUserId)
            => $"{currentUserId}_writingData";

        public async Task<(string Message, bool? IsSuccess)> DeleteOrphanedFiles()
        {
            var lang = await GetLanguage();
            var files = await GetOrphanedFiles();
            if (!files.Any())
            {
                return (LanguageProvider.Admin[lang, "NO_ORPHANED_FILES_DELETED"], true);
            }
            
            var (Succeeded, Failed) = _storageService.BulkDeleteAttachments(files.Select(f => f.PhysicalFilename));

            if (Succeeded?.Any() ?? false)
            {
                var connection = _context.Database.GetDbConnection();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_attachments WHERE attach_id IN @ids",
                    new { ids = files.Where(f => Succeeded.Contains(f.PhysicalFilename)).Select(f => f.AttachId).DefaultIfEmpty() }
                );
            }

            if (Failed?.Any() ?? false)
            {
                return (string.Format(LanguageProvider.Admin[lang, "SOME_ORPHANED_FILES_DELETED_FORMAT"], string.Join(",", Succeeded), string.Join(",", Failed)), false);
            }
            else
            {
                return (string.Format(LanguageProvider.Admin[lang, "ORPHANED_FILES_DELETED_FORMAT"], string.Join(",", Succeeded)), true);
            }
        }

        public async Task<List<PhpbbBbcodes>> GetCustomBBCodes()
            => (await _context.Database.GetDbConnection().QueryAsync<PhpbbBbcodes>("SELECT * FROM phpbb_bbcodes")).AsList();

        public async Task<string> PrepareTextForSaving(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            var connection = _context.Database.GetDbConnection();
            foreach (var sr in await connection.QueryAsync<PhpbbSmilies>("SELECT * FROM phpbb_smilies"))
            {
                var regex = new Regex(@$"(?<=(^|\s)){Regex.Escape(sr.Code)}(?=($|\s))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);
                var replacement = $"<!-- s{sr.Code} --><img src=\"./images/smilies/{sr.SmileyUrl.Trim('/')}\" alt=\"{sr.Code}\" title=\"{sr.Emotion}\" /><!-- s{sr.Code} -->";
                text = regex.Replace(text, replacement);
            }
            return text;
        }

        public string CleanBbTextForDisplay(string text, string uid)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

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

        public string ToCamelCaseJson<T>(T @object)
            => JsonConvert.SerializeObject(
                @object,
                Formatting.None,
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
            );
    }
}
