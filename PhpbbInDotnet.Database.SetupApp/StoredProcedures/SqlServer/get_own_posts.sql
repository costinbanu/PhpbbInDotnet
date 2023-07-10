/****** Object:  StoredProcedure [dbo].[get_own_posts]    Script Date: 03.07.2023 20:37:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[get_own_posts] (@user_id int, @restricted_forum_list nvarchar(max), @skip int, @take int)
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @restricted_forums TABLE(forum_id int INDEX ix_forum_id CLUSTERED);
	INSERT INTO @restricted_forums
	SELECT value FROM string_split(@restricted_forum_list, ',');

	DECLARE @total_count int;
	SELECT @total_count = count(DISTINCT p.topic_id)
	  FROM phpbb_posts p
	  LEFT JOIN @restricted_forums rf ON p.forum_id = rf.forum_id
	 WHERE p.poster_id = @user_id AND rf.forum_id IS NULL;

    WITH own_topics AS (
		SELECT DISTINCT p.topic_id, t.topic_last_post_time
		  FROM phpbb_posts p
		  JOIN phpbb_topics t ON p.topic_id = t.topic_id
		  LEFT JOIN @restricted_forums rf ON p.forum_id = rf.forum_id
		 WHERE p.poster_id = @user_id AND rf.forum_id IS NULL
	 	 ORDER BY t.topic_last_post_time DESC
		OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
	)
	SELECT t.topic_id, 
			t.forum_id,
			t.topic_title, 
			count(p.post_id) AS post_count,
			t.topic_views AS view_count,
			t.topic_type,
			t.topic_last_poster_id,
			t.topic_last_poster_name,
			t.topic_last_post_time,
			t.topic_last_poster_colour,
			t.topic_last_post_id,
			@total_count AS total_count
		FROM phpbb_posts p
		JOIN own_topics ot
		ON p.topic_id = ot.topic_id
		JOIN phpbb_topics t
		ON t.topic_id = ot.topic_id
		GROUP BY t.topic_id, t.topic_title, t.forum_id, t.topic_views, t.topic_type, t.topic_last_poster_id, t.topic_last_poster_name, t.topic_last_post_time, t.topic_last_poster_colour, t.topic_last_post_id
		ORDER BY t.topic_last_post_time DESC
END
GO

