using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class ModeratorService
    {
        private readonly ForumDbContext _context;

        public ModeratorService(ForumDbContext context)
        {
            _context = context;
        }

        public async Task<(string Message, bool? IsSuccess)> ChangeTopicType(int topicId, TopicType topicType)
        {
            try
            {
                var topic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == topicId);
                if (topic != null && topic.TopicType != topicType)
                {
                    topic.TopicType = topicType;
                    await _context.SaveChangesAsync();
                    return ("Subiectul a fost modificat cu succes!", true);
                }
                else if (topic == null)
                {
                    return ("Subiectul nu există.", false);
                }
                else
                {
                    return ("Subiectul are deja tipul solicitat.", false);
                }
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        //public async Task<(string Message, bool? IsSuccess)> ApplyPostAction(IEnumerable<int> posts, ModeratorPostActions action)
        //{

        //}
    }
}
