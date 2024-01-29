CREATE PROCEDURE `sync_topic_with_posts`(topic_ids text, anonymous_user_id int, anonymous_username nvarchar(255), default_user_colour nvarchar(6))
BEGIN

	IF topic_ids = null OR ltrim(rtrim(topic_ids)) = '' THEN
		SET topic_ids = '';
	END IF;
    
	DROP TEMPORARY TABLE IF EXISTS topic_ids_tmp;
    CREATE TEMPORARY TABLE topic_ids_tmp (INDEX (topic_id)) AS
	SELECT topic_id
      FROM phpbb_topics
	 WHERE FIND_IN_SET(topic_id, topic_ids);
    
    DROP TEMPORARY TABLE IF EXISTS post_counts;
    CREATE TEMPORARY TABLE post_counts (INDEX (topic_id)) AS
	SELECT p.topic_id, count(p.post_id) as post_count
	  FROM phpbb_posts p
	  JOIN topic_ids_tmp tit on p.topic_id = tit.topic_id
     GROUP BY p.topic_id;
     
	DROP TEMPORARY TABLE IF EXISTS last_posts;
    CREATE TEMPORARY TABLE last_posts (INDEX (post_id)) AS
	WITH maxes AS (
		SELECT topic_id, MAX(post_time) AS post_time
		  FROM phpbb_posts
		 GROUP BY topic_id
	)
	SELECT DISTINCT p.*
	  FROM phpbb_posts p
	  JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time;
      
	DROP TEMPORARY TABLE IF EXISTS first_posts;
    CREATE TEMPORARY TABLE first_posts (INDEX (post_id)) AS
	WITH mins AS (
		SELECT topic_id, MIN(post_time) AS post_time
		  FROM phpbb_posts
		 GROUP BY topic_id
	)
	SELECT DISTINCT p.*
	  FROM phpbb_posts p
	  JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time;

	UPDATE phpbb_topics t
      JOIN topic_ids_tmp tit on t.topic_id = tit.topic_id
      JOIN last_posts lp ON t.topic_id = lp.topic_id
      JOIN first_posts fp ON t.topic_id = fp.topic_id
	  LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id and lpu.user_id <> anonymous_user_id
	  LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id and fpu.user_id <> anonymous_user_id
	   SET t.topic_last_post_id = lp.post_id,
		   t.topic_last_poster_id = COALESCE(lpu.user_id, anonymous_user_id),
		   t.topic_last_post_subject = lp.post_subject,
		   t.topic_last_post_time = lp.post_time,
		   t.topic_last_poster_name = COALESCE(lpu.username, lp.post_username, anonymous_username),
		   t.topic_last_poster_colour = COALESCE(lpu.user_colour, default_user_colour),
		   t.topic_first_post_id = fp.post_id,
		   t.topic_first_poster_name = COALESCE(fpu.username, fp.post_username, anonymous_username),
		   t.topic_first_poster_colour = COALESCE(fpu.user_colour, default_user_colour),
		   t.topic_title = fp.post_subject
	 WHERE lp.post_id <> t.topic_last_post_id 
		OR fp.post_id <> t.topic_first_post_id 
		OR BINARY t.topic_title <> BINARY fp.post_subject;

	 UPDATE phpbb_topics t
       JOIN post_counts pc ON t.topic_id = pc.topic_id
	    SET t.topic_replies = pc.post_count,
		    t.topic_replies_real = pc.post_count
	 WHERE pc.post_count <> 0 
	   AND (t.topic_replies <> pc.post_count OR t.topic_replies_real <> pc.post_count);

	 DELETE t
       FROM phpbb_topics t
	   LEFT JOIN post_counts pc ON t.topic_id = pc.topic_id
	  WHERE pc.post_count is null or pc.post_count = 0;

END