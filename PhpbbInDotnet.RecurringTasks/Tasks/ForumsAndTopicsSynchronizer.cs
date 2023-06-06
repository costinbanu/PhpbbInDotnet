﻿using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class ForumsAndTopicsSynchronizer : IRecurringTask
    {
        readonly ISqlExecuter _sqlExecuter;

        public ForumsAndTopicsSynchronizer(ISqlExecuter sqlExecuter)
        {
            _sqlExecuter = sqlExecuter;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var postsHavingWrongForumIdTask = _sqlExecuter.ExecuteAsync(
                @"UPDATE phpbb_posts p
                    JOIN phpbb_topics t ON p.topic_id = t.topic_id
                     SET p.forum_id = t.forum_id
                   WHERE p.forum_id <> t.forum_id");

            var forumsHavingWrongLastPostTask = _sqlExecuter.ExecuteAsync(
                @"UPDATE phpbb_forums f
                    JOIN (
                        WITH maxes AS (
	                        SELECT forum_id, MAX(post_time) AS post_time
	                         FROM phpbb_posts
	                        GROUP BY forum_id
					    )
                        SELECT DISTINCT p.*
						  FROM phpbb_posts p
						  JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time
					) lp ON f.forum_id = lp.forum_id
                  LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id
                   SET f.forum_last_post_id = lp.post_id,
	                   f.forum_last_poster_id = COALESCE(u.user_id, @ANONYMOUS_USER_ID),
                       f.forum_last_post_subject = lp.post_subject,
                       f.forum_last_post_time = lp.post_time,
                       f.forum_last_poster_name = COALESCE(u.username, @ANONYMOUS_USER_NAME),
                       f.forum_last_poster_colour = COALESCE(u.user_colour, @DEFAULT_USER_COLOR)
                 WHERE lp.post_id <> f.forum_last_post_id",
                new
                {
                    Constants.ANONYMOUS_USER_ID,
                    Constants.ANONYMOUS_USER_NAME,
                    Constants.DEFAULT_USER_COLOR
                });

            var topicsHavingWrongLastOrFirstPostTask = _sqlExecuter.ExecuteAsync(
                @"UPDATE phpbb_topics t
                    JOIN (
                        WITH maxes AS (
	                      SELECT topic_id, MAX(post_time) AS post_time
		                    FROM phpbb_posts
		                   GROUP BY topic_id
                        )
	                    SELECT DISTINCT p.*
	                        FROM phpbb_posts p
	                        JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    ) lp ON t.topic_id = lp.topic_id
                    JOIN (
                        WITH mins AS (
	                        SELECT topic_id, MIN(post_time) AS post_time
	                          FROM phpbb_posts
	                         GROUP BY topic_id
                        )
	                    SELECT DISTINCT p.*
	                        FROM phpbb_posts p
	                        JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    ) fp ON t.topic_id = fp.topic_id
                  LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id
                  LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id
                   SET t.topic_last_post_id = lp.post_id,
	                   t.topic_last_poster_id = COALESCE(lpu.user_id, @ANONYMOUS_USER_ID),
                       t.topic_last_post_subject = lp.post_subject,
                       t.topic_last_post_time = lp.post_time,
                       t.topic_last_poster_name = COALESCE(lpu.username, @ANONYMOUS_USER_NAME),
                       t.topic_last_poster_colour = COALESCE(lpu.user_colour, @DEFAULT_USER_COLOR),
                       t.topic_first_post_id = fp.post_id,
                       t.topic_first_poster_name = COALESCE(fpu.username, @ANONYMOUS_USER_NAME),
                       t.topic_first_poster_colour = COALESCE(fpu.user_colour, @DEFAULT_USER_COLOR)
                 WHERE lp.post_id <> t.topic_last_post_id OR fp.post_id <> t.topic_first_post_id",
                new
                {
                    Constants.ANONYMOUS_USER_ID,
                    Constants.ANONYMOUS_USER_NAME,
                    Constants.DEFAULT_USER_COLOR
                });

            await Task.WhenAll(postsHavingWrongForumIdTask, forumsHavingWrongLastPostTask, topicsHavingWrongLastOrFirstPostTask);
        }
    }
}
