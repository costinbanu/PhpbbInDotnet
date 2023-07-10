/****** Object:  StoredProcedure [dbo].[sync_last_posts]    Script Date: 03.07.2023 20:44:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[sync_last_posts] (@anonymous_user_id int, @anonymous_username nvarchar(255), @default_user_colour nvarchar(6))
AS
BEGIN
    SET NOCOUNT ON;

	WITH maxes AS (
		SELECT forum_id, MAX(post_time) AS post_time
		  FROM phpbb_posts
		 GROUP BY forum_id
	),
	last_posts AS (
		SELECT DISTINCT p.*
		  FROM phpbb_posts p
		  JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time
	)
	UPDATE f
	SET f.forum_last_post_id = lp.post_id,
		f.forum_last_poster_id = COALESCE(u.user_id, @anonymous_user_id),
		f.forum_last_post_subject = lp.post_subject,
		f.forum_last_post_time = lp.post_time,
		f.forum_last_poster_name = COALESCE(u.username, @anonymous_username),
		f.forum_last_poster_colour = COALESCE(u.user_colour, @default_user_colour)
	FROM phpbb_forums f
	JOIN last_posts lp ON f.forum_id = lp.forum_id
	LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id
	WHERE lp.post_id <> f.forum_last_post_id;

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
		t.topic_first_poster_colour = COALESCE(fpu.user_colour, @default_user_colour)
	FROM phpbb_topics t
	JOIN last_posts lp ON t.topic_id = lp.topic_id
	JOIN first_posts fp ON t.topic_id = fp.topic_id
	LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id
	LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id
	WHERE lp.post_id <> t.topic_last_post_id OR fp.post_id <> t.topic_first_post_id;
END
GO

