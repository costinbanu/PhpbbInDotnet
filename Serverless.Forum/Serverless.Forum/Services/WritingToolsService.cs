using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class WritingToolsService
    {
        private readonly IConfiguration _config;
        private readonly StorageService _storageService;
        private readonly CacheService _cacheService;
        private readonly Utils _utils;

        public WritingToolsService(IConfiguration config, StorageService storageService, CacheService cacheService, Utils utils)
        {
            _config = config;
            _storageService = storageService;
            _cacheService = cacheService;
            _utils = utils;
        }

        public async Task<List<PhpbbWords>> GetBannedWords()
        {
            using var context = new ForumDbContext(_config);
            return await context.PhpbbWords.AsNoTracking().ToListAsync();
        }

        public async Task<(string Message, bool? IsSuccess)> ManageBannedWords(List<PhpbbWords> words, List<int> indexesToRemove)
        {
            try
            {
                using var context = new ForumDbContext(_config);

                var newWords = from w2 in words
                               join w in context.PhpbbWords.AsNoTracking()
                               on w2.WordId equals w.WordId
                               into joined
                               from j in joined.DefaultIfEmpty()
                               where j == null && !string.IsNullOrWhiteSpace(w2.Word) && !string.IsNullOrWhiteSpace(w2.Replacement)
                               select w2;
                
                if (newWords.Any())
                {
                    await context.PhpbbWords.AddRangeAsync(newWords);
                }

                var toRemove = new List<PhpbbWords>();
                foreach (var idx in indexesToRemove)
                {
                        toRemove.Add(await context.PhpbbWords.FirstOrDefaultAsync(x => x.WordId == words[idx].WordId));
                }
                context.PhpbbWords.RemoveRange(toRemove);
                
                var editedWords = from w in await context.PhpbbWords.ToListAsync()
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

                await context.SaveChangesAsync();

                return ("Cuvintele au fost actualizate cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare! Mai incearca!", false);
            }
        }

        public async Task<(IEnumerable<Tuple<S3Object, string>> inS3, IEnumerable<Tuple<PhpbbAttachments, string>> inDb)> CacheOrphanedFiles(int currentUserId)
        {
            string getFileName(string s3Path)
                => !string.IsNullOrWhiteSpace(_storageService.FolderPrefix) ? s3Path.Replace(_storageService.FolderPrefix, string.Empty) : s3Path;

            int getUserId(string filename)
            {
                var cleanName = !string.IsNullOrWhiteSpace(_storageService.FolderPrefix) ? filename.Replace(_storageService.FolderPrefix, string.Empty) : filename;
                if (!cleanName.Contains('_'))
                {
                    return 1;
                }
                return int.TryParse(cleanName.Split('_')[0], out var val) ? val : 1;
            }

            using var context = new ForumDbContext(_config);
            var inS3 = (
                from s in await _storageService.ListItems().ToListAsync()
                
                let name = getFileName(s.Key)
                
                join u in context.PhpbbUsers.AsNoTracking()
                on getUserId(name) equals u.UserId
                into joinedUsers
                
                from ju in joinedUsers.DefaultIfEmpty()
                where !string.IsNullOrWhiteSpace(name) && !name.StartsWith(_storageService.AvatarsFolder)
                select Tuple.Create(s, ju == null ? "Anonymous" : ju.Username)
            ).ToList();

            var inDb = await (
                from a in context.PhpbbAttachments.AsNoTracking()
                
                join u in context.PhpbbUsers.AsNoTracking()
                on a.PosterId equals u.UserId
                into joinedUsers
                
                from ju in joinedUsers.DefaultIfEmpty()
                select new { Attach = a, User = ju == null ? "Anonymous" : ju.Username }
            ).ToListAsync();

            var toReturn = (
                inS3: from s in inS3

                      join d in inDb
                      on getFileName(s.Item1.Key) equals d.Attach.PhysicalFilename
                      into joined

                      from j in joined.DefaultIfEmpty()
                      where j == null && (string.IsNullOrWhiteSpace(_storageService.FolderPrefix) || s.Item1.Key.StartsWith(_storageService.FolderPrefix))
                      select s,

                inDb: from d in inDb

                      join s in inS3
                      on d.Attach.PhysicalFilename equals getFileName(s.Item1.Key)
                      into joined

                      from j in joined.DefaultIfEmpty()
                      where j == null
                      select Tuple.Create(d.Attach, d.User)
            );

            await _cacheService.SetInCacheAsync(
                GetCacheKey(currentUserId), 
                (toReturn.inS3.Select(x => x.Item1.Key), toReturn.inDb.Select(x => x.Item1.AttachId))
            );

            return toReturn;
        }

        public string GetCacheKey(int currentUserId)
            => $"{currentUserId}_writingData";

        public async Task<(string Message, bool? IsSuccess)> DeleteS3OrphanedFiles(IEnumerable<string> s3Files)
        {
            var success = ($"{s3Files.Count()} fișiere au fost șterse de pe server cu succes!", true);
            
            if(!s3Files.Any())
            {
                return success;
            }
            
            if (await _storageService.BulkDeleteFiles(s3Files))
            {
                return success;
            }
            
            return ("A intervenit o eroare, mai încearcă o dată.", false);
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
                using var context = new ForumDbContext(_config);
                context.PhpbbAttachments.RemoveRange(
                    from a in context.PhpbbAttachments
                    join d in dbFiles
                    on a.AttachId equals d
                    select a
                );
                await context.SaveChangesAsync();
                return success;
            }
            catch
            {
                return ("A intervenit o eroare, mai încearcă o dată.", false);
            }
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
    }
}
