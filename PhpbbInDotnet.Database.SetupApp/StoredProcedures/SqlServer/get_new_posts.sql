/****** Object:  StoredProcedure [dbo].[get_new_posts]    Script Date: 03.07.2023 20:36:24 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[get_new_posts] (@topic_list nvarchar(max), @restricted_forum_list nvarchar(max), @skip int, @take int)
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @allowed_topics TABLE(topic_id int INDEX ix_forum_id CLUSTERED);
	INSERT INTO @allowed_topics
	SELECT value FROM string_split(@topic_list, ',');	

	DECLARE @restricted_forums TABLE(forum_id int INDEX ix_forum_id CLUSTERED);
	INSERT INTO @restricted_forums
	SELECT value FROM string_split(@restricted_forum_list, ',');	

    SELECT t.topic_id, 
		   t.forum_id,
		   t.topic_title, 
		   count(p.post_id) AS post_count,
		   t.topic_views AS view_count,
		   t.topic_last_poster_id,
		   t.topic_last_poster_name,
		   t.topic_last_post_time,
		   t.topic_last_poster_colour,
		   t.topic_last_post_id
	  FROM phpbb_topics t
	  JOIN phpbb_posts p ON t.topic_id = p.topic_id
	  JOIN @allowed_topics [at] ON [at].topic_id = t.topic_id
	  LEFT JOIN @restricted_forums rf ON rf.forum_id = t.forum_id
	 WHERE rf.forum_id IS NULL
	 GROUP BY t.topic_id, t.topic_title, t.forum_id, t.topic_views, t.topic_last_poster_id, t.topic_last_poster_name, t.topic_last_post_time, t.topic_last_poster_colour, t.topic_last_post_id
	 ORDER BY t.topic_last_post_time DESC
	 OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
END
GO

