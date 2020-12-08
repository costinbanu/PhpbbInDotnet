using Dapper;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class ModeratorService
    {
        private readonly ForumDbContext _context;
        private readonly PostService _postService;
        private readonly CommonUtils _utils;

        public ModeratorService(ForumDbContext context, PostService postService, CommonUtils utils)
        {
            _context = context;
            _postService = postService;
            _utils = utils;
        }

        #region Topic

        public async Task<(string Message, bool? IsSuccess)> ChangeTopicType(int topicId, TopicType topicType)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var rows = await conn.ExecuteAsync("UPDATE phpbb_topics SET topic_type = @topicType WHERE topic_id = @topicId", new { topicType, topicId });

                if (rows == 1)
                {
                    return ("Subiectul a fost modificat cu succes!", true);
                }
                else
                {
                    return ($"Subiectul {topicId} nu există.", false);
                }

            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return ($"A intervenit o eroare, încearcă din nou. ID: {id}", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MoveTopic(int topicId, int destinationForumId)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var topicRows = await conn.ExecuteAsync(
                    "UPDATE phpbb_topics SET forum_id = @destinationForumId WHERE topic_id = @topicID AND EXISTS(SELECT 1 FROM phpbb_forums WHERE forum_id = @destinationForumId)",
                    new { topicId, destinationForumId }
                );

                if (topicRows == 0)
                {
                    return ("Subiectul sau forumul de destinație nu există", false);
                }

                var oldPosts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC", new { topicId })).AsList();
                await conn.ExecuteAsync(
                    "UPDATE phpbb_posts SET forum_id = @destinationForumId WHERE topic_id = @topicId; " +
                    "UPDATE phpbb_topics_track SET forum_id = @destinationForumId WHERE topic_id = @topicId", 
                    new { destinationForumId, topicId }
                );
                foreach (var post in oldPosts)
                {
                    await _postService.CascadePostDelete(post, true);
                    post.ForumId = destinationForumId;
                    await _postService.CascadePostAdd(post, true);
                }

                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return ($"A intervenit o eroare, încearcă din nou. ID: {id}", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> LockUnlockTopic(int topicId, bool @lock)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var rows = await conn.ExecuteAsync("UPDATE phpbb_topics SET topic_status = @status WHERE topic_id = @topicId", new { status = @lock.ToByte(), topicId });

                if (rows == 0)
                {
                    return ($"Subiectul {topicId} nu există", false);
                }
                
                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }
        
        public async Task<(string Message, bool? IsSuccess)> DeleteTopic(int topicId)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId", new { topicId })).AsList();
                
                if (!posts.Any())
                {
                    return ($"Subiectul {topicId} nu există", false);
                }

                await conn.ExecuteAsync("DELETE FROM phpbb_posts WHERE topic_id = @topicId", new { topicId });
                posts.ForEach(async (p) => await _postService.CascadePostDelete(p, false));

                return ("Subiectul a fost șters cu succes!", true);
            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return ($"A intervenit o eroare, încearcă din nou. ID: {id}", false);
            }
        }

        #endregion Topic

        #region Post

        public async Task<(string Message, bool? IsSuccess)> SplitPosts(int[] postIds, int? destinationForumId)
        {
            try
            {
                if ((destinationForumId ?? 0) == 0)
                {
                    return ("Forumul destinație nu este valid.", false); 
                }

                if (!(postIds?.Any() ?? false))
                {
                    return ("Această acțiune necesită măcar un mesaj selectat.", false);
                }

                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();

                if (posts.Count != postIds.Length)
                {
                    return ("Cel puțin un mesaj dintre cele selectate a fost mutat sau șters între timp.", false);
                }

                var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @title, @time); " +
                    "SELECT * FROM phpbb_topics WHERE topic_id = LAST_INSERT_ID();",
                    new { forumId = destinationForumId.Value, title = posts.First().PostSubject, time = posts.First().PostTime }
                );
                var oldTopicId = posts.First().TopicId;

                await conn.ExecuteAsync("UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", new { curTopic.TopicId, curTopic.ForumId, postIds });

                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false);
                    post.TopicId = curTopic.TopicId;
                    post.ForumId = curTopic.ForumId;
                    await _postService.CascadePostAdd(post, false);
                }
                
                return ("Mesajele au fost separate cu succes!", true);
            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return ($"A intervenit o eroare, încearcă din nou. ID: {id}", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MovePosts(int[] postIds, int? destinationTopicId)
        {
            try
            {
                if ((destinationTopicId ?? 0) == 0)
                {
                    return ("Subiectul destinație nu este valid.", false);
                }

                if (!(postIds?.Any() ?? false))
                {
                    return ("Această acțiune necesită măcar un mesaj selectat.", false);
                }

                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return ("Cel puțin un mesaj dintre cele selectate a fost mutat sau șters între timp.", false);
                }

                var newTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @destinationTopicId", new { destinationTopicId });
                if (newTopic == null)
                {
                    return ("Subiectul de destinație nu există", false);
                }

                await conn.ExecuteAsync("UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", new { newTopic.TopicId, newTopic.ForumId, postIds });

                var oldTopicId = posts.First().TopicId;
                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false);
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                    await _postService.CascadePostAdd(post, false);
                }

                return ("Mesajele au fost mutate cu succes!", true);
            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return ($"A intervenit o eroare, încearcă din nou. ID: {id}", false);
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

                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return ("Cel puțin un mesaj dintre cele selectate a fost mutat sau șters între timp.", false);
                }

                await conn.ExecuteAsync("DELETE FROM phpbb_posts WHERE post_id IN @postIds", new { postIds });
                posts.ForEach(async (p) => await _postService.CascadePostDelete(p, false));

                return ("Subiectul a fost modificat cu succes!", true);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return ("A intervenit o eroare, încearcă din nou.", false);
            }
        }

        #endregion Post

        public async Task<IEnumerable<Tuple<int, string>>> GetReportedMessages()
        {
            var connection = _context.Database.GetDbConnection();
            return (await connection.QueryAsync(
                @"SELECT r.post_id, jr.reason_title
                    FROM phpbb_reports r
                    JOIN phpbb_reports_reasons jr ON r.reason_id = jr.reason_id
                    WHERE r.report_closed = 0"
            )).Select(r => Tuple.Create((int)r.post_id, (string)r.reason_title));
        }
    }
}
