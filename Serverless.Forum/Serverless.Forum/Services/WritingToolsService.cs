using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class WritingToolsService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public WritingToolsService(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
        }

        public async Task<List<PhpbbWords>> GetBannedWords()
        {
            using (var context = new ForumDbContext(_config))
            {
                return await context.PhpbbWords.AsNoTracking().ToListAsync();
            }
        }

        public async Task<(string Message, bool? IsSuccess)> ManageBannedWords(IEnumerable<PhpbbWords> words, IEnumerable<int> toRemove)
        {
            try
            {
                using (var context = new ForumDbContext(_config))
                {
                    var newWords = from w2 in words
                                   join w in context.PhpbbWords
                                   on w2.WordId equals w.WordId
                                   into joined
                                   from j in joined.DefaultIfEmpty()
                                   where j == null
                                   select w2;

                    var editedWords = from w in context.PhpbbWords
                                      join w2 in words on w.WordId equals w2.WordId
                                      where w.Word != w2.Word || w.Replacement != w2.Replacement
                                      select new { Old = w, New = w2 };

                    await context.PhpbbWords.AddRangeAsync(newWords);

                    foreach (var word in editedWords)
                    {
                        word.Old.Replacement = word.New.Replacement;
                        word.New.Word = word.New.Word;
                    }

                    await context.SaveChangesAsync();
                }
                return ("Cuvintele au fost actualizate cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare! Mai incearca!", false);
            }
        }
    }
}
