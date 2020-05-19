using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class ModeratorService
    {
        private readonly ForumDbContext _context;
        private readonly UserService _userService;
        private readonly PostService _postService;

        public ModeratorService(ForumDbContext context, UserService userService, PostService postService)
        {
            _context = context;
            _userService = userService;
            _postService = postService;
        }

        #region Topic

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
                    return ($"Subiectul {topicId} nu există.", false);
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

        public async Task<(string Message, bool? IsSuccess)> MoveTopic(int topicId, int destinationForumId)
        {
            try
            {
                var curTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == topicId);
                if (curTopic == null)
                {
                    return ($"Subiectul {topicId} nu există", false);
                }
                var curParent = await _context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);
                var posts = await _context.PhpbbPosts.Where(p => p.TopicId == curTopic.TopicId).OrderBy(p => p.PostTime).ToListAsync();
                
                curTopic.ForumId = destinationForumId;
                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(_context, post, true);
                    post.ForumId = destinationForumId;
                }             
                
                var newParent = await _context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == destinationForumId);
                await _postService.CascadePostAdd(_context, posts.Last(), await _userService.GetLoggedUserById(posts.Last().PosterId), false, true);
                await _context.SaveChangesAsync();

                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> LockUnlockTopic(int topicId, bool @lock)
        {
            try
            {
                var curTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == topicId);
                if (curTopic == null)
                {
                    return ($"Subiectul {topicId} nu există", false);
                }
                
                curTopic.TopicStatus = (byte)(@lock ? 1 : 0);
                await _context.SaveChangesAsync();
                
                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeleteTopic(int topicId)
        {
            try
            {
                var curTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == topicId);
                if (curTopic == null)
                {
                    return ($"Subiectul {topicId} nu există", false);
                }
                var posts = await _context.PhpbbPosts.Where(p => p.TopicId == curTopic.TopicId).ToListAsync();
                _context.PhpbbPosts.RemoveRange(posts);
                posts.ForEach(async (p) => await _postService.CascadePostDelete(_context, p, false));
                await _context.SaveChangesAsync();
                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        #endregion Topic

        #region Post

        public async Task<(string Message, bool? IsSuccess)> SplitPosts(int[] postIds, int destinationForumId)
        {
            try
            {
                if (!(postIds?.Any() ?? false))
                {
                    return ("Această acțiune necesită măcar un mesaj selectat.", false);
                }

                var posts = await _context.PhpbbPosts.Where(p => postIds.Contains(p.PostId)).OrderBy(p => p.PostTime).ToListAsync();
                if (posts.Count != postIds.Length)
                {
                    return ("Cel puțin un mesaj dintre cele selectate a fost mutat sau șters între timp.", false);
                }

                var topicResult = await _context.PhpbbTopics.AddAsync(new PhpbbTopics
                {
                    ForumId = destinationForumId,
                    TopicTitle = posts.First().PostSubject,
                    TopicTime = posts.First().PostTime
                });
                topicResult.Entity.TopicId = 0;
                await _context.SaveChangesAsync();
                var curTopic = topicResult.Entity;

                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(_context, post, false);
                    post.TopicId = curTopic.TopicId;
                    post.ForumId = curTopic.ForumId;
                }

                await _postService.CascadePostAdd(_context, posts.First(), await _userService.GetLoggedUserById(posts.First().PosterId), true, false);
                await _postService.CascadePostAdd(_context, posts.Last(), await _userService.GetLoggedUserById(posts.Last().PosterId), false, false);
                await _context.SaveChangesAsync();
                
                return ("Mesajele au fost separate cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MovePosts(int[] postIds, int destinationTopicId)
        {
            try
            {
                if (!(postIds?.Any() ?? false))
                {
                    return ("Această acțiune necesită măcar un mesaj selectat.", false);
                }

                var posts = await _context.PhpbbPosts.Where(p => postIds.Contains(p.PostId)).OrderBy(p => p.PostTime).ToListAsync();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return ("Cel puțin un mesaj dintre cele selectate a fost mutat sau șters între timp.", false);
                }

                var curTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == posts.First().TopicId);
                var newTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == destinationTopicId);
                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(_context, post, false);
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                }

                await _postService.CascadePostAdd(_context, posts.First(), await _userService.GetLoggedUserById(posts.First().PosterId), true, false);
                await _postService.CascadePostAdd(_context, posts.Last(), await _userService.GetLoggedUserById(posts.Last().PosterId), false, false);
                await _context.SaveChangesAsync();
                asta muta tot subiectul, care a disparut cu totul? weird
                return ("Mesajele au fost mutate cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }


        public async Task<(string Message, bool? IsSuccess)> DeletePosts(int[] postIds)
        {
            try
            {
                if (!(postIds?.Any() ?? false))
                {
                    return ("Această acțiune necesită măcar un mesaj selectat.", false);
                }

                var posts = await _context.PhpbbPosts.Where(p => postIds.Contains(p.PostId)).OrderBy(p => p.PostTime).ToListAsync();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return ("Cel puțin un mesaj dintre cele selectate a fost mutat sau șters între timp.", false);
                }

                var curTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == posts.First().TopicId);
                _context.PhpbbPosts.RemoveRange(posts);
                posts.ForEach(async (p) => await _postService.CascadePostDelete(_context, p, false));
                
                await _context.SaveChangesAsync();

                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        #endregion Post
    }
}
