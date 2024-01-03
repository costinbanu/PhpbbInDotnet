SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE sync_topic_with_posts (@topic_id int, @anonymous_user_id int, @anonymous_username nvarchar(255), @default_user_colour nvarchar(6))
AS
BEGIN
	SET NOCOUNT ON;
	
	DECLARE @post_count int = (
		SELECT COUNT(post_id)
		  FROM phpbb_posts
		 WHERE topic_id = @topic_id);

	WITH last_post AS (
		SELECT TOP 1 *
		  FROM phpbb_posts
		 WHERE topic_id = @topic_id
		 ORDER BY post_time DESC
	),
	first_post AS (
		SELECT TOP 1 *
		  FROM phpbb_posts
		 WHERE topic_id = @topic_id
		 ORDER BY post_time ASC
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
		t.topic_replies = @post_count,
		t.topic_replies_real = @post_count,
		t.topic_title = fp.post_subject
	FROM phpbb_topics t
	JOIN last_post lp ON t.topic_id = lp.topic_id
	JOIN first_post fp ON t.topic_id = fp.topic_id
	LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id
	LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id
	WHERE t.topic_id = @topic_id

END
GO
