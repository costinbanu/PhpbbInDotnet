SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE sync_forum_with_posts (@forum_id int, @anonymous_user_id int, @anonymous_username nvarchar(255), @default_user_colour nvarchar(6))
AS
BEGIN
	SET NOCOUNT ON;
	
	WITH last_post AS (
		SELECT TOP 1 *
		  FROM phpbb_posts
		 WHERE forum_id = @forum_id
		 ORDER BY post_time DESC
	)
	UPDATE f
	SET f.forum_last_post_id = lp.post_id,
		f.forum_last_poster_id = COALESCE(u.user_id, @anonymous_user_id),
		f.forum_last_post_subject = lp.post_subject,
		f.forum_last_post_time = lp.post_time,
		f.forum_last_poster_name = COALESCE(u.username, @anonymous_username),
		f.forum_last_poster_colour = COALESCE(u.user_colour, @default_user_colour)
	FROM phpbb_forums f
	JOIN last_post lp ON f.forum_id = lp.forum_id
	LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id
	WHERE f.forum_id = @forum_id

END
GO
