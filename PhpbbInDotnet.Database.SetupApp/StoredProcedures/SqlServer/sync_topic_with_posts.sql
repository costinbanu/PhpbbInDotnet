SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[sync_topic_with_posts] (@topic_ids nvarchar(max), @anonymous_user_id int, @anonymous_username nvarchar(255), @default_user_colour nvarchar(6))
AS
BEGIN
	SET NOCOUNT ON;

	CREATE TABLE #topic_ids (topic_id int INDEX ix_topic_id CLUSTERED);
	IF (@topic_ids IS NULL OR @topic_ids = '')
		INSERT INTO #topic_ids
		SELECT topic_id
		FROM phpbb_topics;
	ELSE
		INSERT INTO #topic_ids
		SELECT value FROM string_split(@topic_ids, ',');

	CREATE TABLE #post_counts (topic_id int INDEX ix_topic_id CLUSTERED, post_count int);
	INSERT INTO #post_counts
	SELECT p.topic_id, count(p.post_id) as post_count
	  FROM phpbb_posts p
	  JOIN #topic_ids ti on p.topic_id = ti.topic_id
     GROUP BY p.topic_id;

	WITH maxes AS (
		SELECT topic_id, MAX(post_time) AS post_time
		  FROM phpbb_posts
		 GROUP BY topic_id
	),
	last_posts AS (
		SELECT DISTINCT p.*
		  FROM phpbb_posts p
		  JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
	),
	mins AS (
		SELECT topic_id, MIN(post_time) AS post_time
		  FROM phpbb_posts
		 GROUP BY topic_id
	),
	first_posts AS (
		SELECT DISTINCT p.*
		  FROM phpbb_posts p
		  JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
	)
	UPDATE t
	   SET t.topic_last_post_id = lp.post_id,
		   t.topic_last_poster_id = COALESCE(lpu.user_id, @anonymous_user_id),
		   t.topic_last_post_subject = lp.post_subject,
		   t.topic_last_post_time = lp.post_time,
		   t.topic_last_poster_name = COALESCE(lpu.username, @anonymous_username),
		   t.topic_last_poster_colour = COALESCE(lpu.user_colour, @default_user_colour),
		   t.topic_first_post_id = fp.post_id,
		   t.topic_first_poster_name = COALESCE(fpu.username, @anonymous_username),
		   t.topic_first_poster_colour = COALESCE(fpu.user_colour, @default_user_colour),
		   t.topic_title = fp.post_subject
	  FROM phpbb_topics t
	  JOIN #topic_ids ti on t.topic_id = ti.topic_id
	  JOIN last_posts lp ON t.topic_id = lp.topic_id
	  JOIN first_posts fp ON t.topic_id = fp.topic_id
	  LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id
	  LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id
	 WHERE lp.post_id <> t.topic_last_post_id OR fp.post_id <> t.topic_first_post_id OR t.topic_title <> fp.post_subject;

	 UPDATE t
	    SET t.topic_replies = pc.post_count,
		    t.topic_replies_real = pc.post_count
	  FROM phpbb_topics t
	  JOIN #post_counts pc ON t.topic_id = pc.topic_id
	 WHERE pc.post_count <> 0 AND (t.topic_replies <> pc.post_count OR t.topic_replies_real <> pc.post_count);

	 DELETE t
	   FROM phpbb_topics t
	   JOIN #post_counts pc ON t.topic_id = pc.topic_id
	  WHERE pc.post_count = 0;

END
