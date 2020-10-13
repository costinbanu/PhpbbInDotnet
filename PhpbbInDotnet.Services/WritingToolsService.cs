using Dapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    public class WritingToolsService
    {
        private readonly ForumDbContext _context;
        private readonly StorageService _storageService;

        public WritingToolsService(ForumDbContext context, StorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        public async Task<IEnumerable<PhpbbWords>> GetBannedWordsAsync()
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            return await connection.QueryAsync<PhpbbWords>("SELECT * FROM phpbb_words");
        }

        public IEnumerable<PhpbbWords> GetBannedWords()
        {
            using var connection = _context.Database.GetDbConnection();
            connection.OpenIfNeeded();
            return connection.Query<PhpbbWords>("SELECT * FROM phpbb_words");
        }

        public async Task<(string Message, bool? IsSuccess)> ManageBannedWords(List<PhpbbWords> words, List<int> indexesToRemove)
        {
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

                return ("Cuvintele au fost actualizate cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare! Mai incearca!", false);
            }
        }

        public async Task<IEnumerable<AttachmentManagementDto>> GetOrphanedFiles()
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            return await connection.QueryAsync<AttachmentManagementDto>("SELECT a.*, u.username FROM phpbb_attachments a JOIN phpbb_users u on a.poster_id = u.user_id WHERE a.is_orphan = 1");
        }

        public string GetCacheKey(int currentUserId)
            => $"{currentUserId}_writingData";

        public (string Message, bool? IsSuccess) DeleteOrphanedFiles(IEnumerable<string> files)
        {
            if(!files.Any())
            {
                return ($"Nici un fișier nu a fost șters de pe server.", true);
            }
            var (Succeeded, Failed) = _storageService.BulkDeleteAttachments(files);
            if (Failed.Any())
            {
                return ($"Fișierele {string.Join(",", Succeeded)} au fost șterse cu succes, însă fișierele {string.Join(",", Failed)} nu au fost șterse.", false);
            }
            else
            {
                return ($"Fișierele {string.Join(",", Succeeded)} au fost șterse cu succes.", true);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeleteDbOrphanedFiles(IEnumerable<int> dbFiles)
        {
            var success = ($"{dbFiles.Count()} fișiere au fost șterse din baza de date cu succes!", true); ;

            if (!dbFiles.Any())
            {
                return success;
            }

            try
            {
                _context.PhpbbAttachments.RemoveRange(
                    from a in _context.PhpbbAttachments
                    join d in dbFiles
                    on a.AttachId equals d
                    select a
                );
                await _context.SaveChangesAsync();
                return success;
            }
            catch
            {
                return ("A intervenit o eroare, mai încearcă o dată.", false);
            }
        }

        public string PrepareTextForSaving(string text)
        {
            foreach (var sr in _context.PhpbbSmilies.AsNoTracking().ToList())
            {
                var regex = new Regex(@$"(?<=(^|\s)){Regex.Escape(sr.Code)}(?=($|\s))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

            var uidRegex = new Regex($":{uid}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var tagRegex = new Regex(@"(:[a-z])(\]|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var cleanTextTemp = string.IsNullOrWhiteSpace(uid) ? HttpUtility.HtmlDecode(text) : uidRegex.Replace(HttpUtility.HtmlDecode(text), string.Empty);
            var noUid = tagRegex.Replace(cleanTextTemp, "$2");

            string replace(string input, string pattern, int groupsIndex)
            {
                var output = input;
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
            var noLinks = replace(noSmileys, @"<!-- m --><a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1.+?<!-- m -->", 2);
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
