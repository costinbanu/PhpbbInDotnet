using Dapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class WritingToolsService
    {
        private readonly ForumDbContext _context;
        private readonly StorageService _storageService;
        private readonly CacheService _cacheService;
        private readonly Utils _utils;

        public WritingToolsService(ForumDbContext context, StorageService storageService, CacheService cacheService, Utils utils)
        {
            _context = context;
            _storageService = storageService;
            _cacheService = cacheService;
            _utils = utils;
        }

        public async Task<IEnumerable<PhpbbWords>> GetBannedWords()
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            return await connection.QueryAsync<PhpbbWords>("SELECT * FROM phpbb_words");
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

        public async Task<IEnumerable<Tuple<FileInfo, string>>> CacheOrphanedFiles(int currentUserId)
        //public async Task<(IEnumerable<Tuple<FileInfo, string>> onDisk, IEnumerable<Tuple<PhpbbAttachments, string>> inDb)> CacheOrphanedFiles(int currentUserId)
        {
            int getUserId(string filename)
            {
                if (!filename.Contains('_'))
                {
                    return 1;
                }
                return int.TryParse(filename.Split('_')[0], out var val) ? val : 1;
            }

            var files = _storageService.ListAttachments();
            IEnumerable<dynamic> users;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                users = await connection.QueryAsync("SELECT user_id, username FROM phpbb_users");
            }

            var inS3 = from s in files

                       join u in users
                       on getUserId(s.Name) equals (int)u.user_id
                       into joinedUsers

                       from ju in joinedUsers.DefaultIfEmpty()
                       where s != null
                       select Tuple.Create(s, (string)ju?.username ?? "Anonymous");


            var inDb = await (
                from a in _context.PhpbbAttachments.AsNoTracking()

                join u in _context.PhpbbUsers.AsNoTracking()
                on a.PosterId equals u.UserId
                into joinedUsers

                from ju in joinedUsers.DefaultIfEmpty()
                select new { Attach = a, User = ju == null ? "Anonymous" : ju.Username }
            ).ToListAsync();

            var toReturn = /*(
                inS3:*/ from s in inS3

                      join d in inDb
                      on s.Item1.Name equals d.Attach.PhysicalFilename
                      into joined

                      from j in joined.DefaultIfEmpty()
                      where j == null
                      select s/*,

                inDb: from d in inDb

                      join s in inS3
                      on d.Attach.PhysicalFilename equals s.Item1.Name
                      into joined

                      from j in joined.DefaultIfEmpty()
                      where j == null
                      select Tuple.Create(d.Attach, d.User)
            )*/;

            await _cacheService.SetInCache(
                GetCacheKey(currentUserId),
                (toReturn/*.inS3*/.Select(x => x.Item1.Name)/*, toReturn.inDb.Select(x => x.Item1.AttachId)*/)
            );

            return toReturn;
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
                return ($"Fișierele {string.Join(',', Succeeded)} au fost șterse cu succes, însă fișierele {string.Join(',', Failed)} nu au fost șterse.", false);
            }
            else
            {
                return ($"Fișierele {string.Join(',', Succeeded)} au fost șterse cu succes.", true);
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

            var urlRegex = new Regex(@"(?<=(^|\s))(ftp:\/\/|www\.|https?:\/\/){1}[a-zA-Z0-9u00a1-\uffff0-]{2,}\.[a-zA-Z0-9u00a1-\uffff0-]{2,}($|\S*)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
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

        public string CleanBbTextForDisplay(string text, string uid)
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

        public string ToCamelCaseJson<T>(T @object)
            => JsonConvert.SerializeObject(
                @object,
                Formatting.None,
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
            );
    }
}
