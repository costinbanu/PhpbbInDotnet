SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[sync_forum_with_posts] (@forum_ids nvarchar(max), @anonymous_user_id int, @anonymous_username nvarchar(255), @default_user_colour nvarchar(6))
AS
BEGIN
	SET NOCOUNT ON;

	CREATE TABLE #forum_ids (forum_id int INDEX ix_forum_id CLUSTERED);
	IF (@forum_ids IS NULL OR ltrim(rtrim(@forum_ids)) = '')
		INSERT INTO #forum_ids
		SELECT forum_id
		FROM phpbb_forums;
	ELSE
		INSERT INTO #forum_ids
		SELECT value FROM string_split(@forum_ids, ',');
	
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
		   f.forum_last_poster_name = COALESCE(u.username, lp.post_username, @anonymous_username),
		   f.forum_last_poster_colour = COALESCE(u.user_colour, @default_user_colour)
	  FROM phpbb_forums f
	  JOIN last_posts lp ON f.forum_id = lp.forum_id
	  JOIN #forum_ids fi ON f.forum_id = fi.forum_id
	  LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id and u.user_id <> @anonymous_user_id
	 WHERE lp.post_id <> f.forum_last_post_id;

END
