/****** Object:  StoredProcedure [dbo].[get_posts]    Script Date: 03.07.2023 20:40:16 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[get_posts] (@topic_id int, @anonymous_user_id int, @order nvarchar(20), @skip int, @take int)
AS
BEGIN
	SET NOCOUNT ON;

	IF (@order <> 'ASC' AND @order <> 'DESC')
		SET @order = 'ASC';

	DECLARE @total_count int;
	SET @total_count = (
		SELECT count(1) 
		  FROM phpbb_posts 
		 WHERE topic_id = @topic_id);

    SELECT p.forum_id,
		   p.topic_id,
		   p.post_id,
		   p.post_subject,
		   p.post_text,
		   p.poster_id as author_id,
		   p.bbcode_uid,
		   p.post_time,
		   p.post_edit_count,
		   p.post_edit_reason,
		   p.post_edit_time,
		   p.poster_ip as [ip],
		   p.post_username,
		   p.post_edit_user
	  INTO #posts
	  FROM phpbb_posts p
	 WHERE topic_id = @topic_id 
	 ORDER BY 
	  CASE WHEN @order = 'ASC' THEN post_time END ASC, 
	  CASE WHEN @order = 'DESC' THEN post_time END DESC
	OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;

	SELECT DISTINCT u.user_id, 
		   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
		   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
	  INTO #ranks
	  FROM phpbb_users u
	  JOIN phpbb_groups g ON u.group_id = g.group_id
	  JOIN #posts p on p.author_id = u.user_id
	  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
	  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id

	SELECT p.*, 
		   CASE WHEN p.author_id = @anonymous_user_id THEN p.post_username ELSE a.username END AS author_name,
		   a.user_colour AS author_color,
		   a.user_avatar AS author_avatar,
		   e.username AS post_edit_user,
		   r.rank_title,
		   @total_count AS total_count
	  FROM #posts p
	  LEFT JOIN #ranks r ON p.author_id = r.user_id
	  LEFT JOIN phpbb_users a ON p.author_id = a.user_id
	  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
	 ORDER BY 
	  CASE WHEN @order = 'ASC' THEN post_time END ASC, 
	  CASE WHEN @order = 'DESC' THEN post_time END DESC;
END
GO

