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
    public class ModeratorService
    {
        private readonly IConfiguration _config;

        public ModeratorService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<(string Message, bool? IsSuccess)> ChangeTopicType(int topicId, TopicType topicType)
        {
            try
            {
                using var context = new ForumDbContext(_config);
                var topic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == topicId);
                if (topic != null && topic.TopicType != topicType)
                {
                    topic.TopicType = topicType;
                    await context.SaveChangesAsync();
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
    }
}
