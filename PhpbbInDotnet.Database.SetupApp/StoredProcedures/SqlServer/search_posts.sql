USE [forum]
GO
/****** Object:  StoredProcedure [dbo].[search_posts]    Script Date: 06.07.2023 21:54:03 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[search_posts]
(
    @ANONYMOUS_USER_ID int = 1,
    @topic_id int = 0,
	@author_id int = 0,
	@search_text nvarchar(300) = '',
	@searchable_forums nvarchar(max) = '',
	@skip int = 0,
	@take int = 14
)
AS
BEGIN
    SET NOCOUNT ON;

	CREATE TABLE #searchable_forums_table (forum_id int INDEX ix_forum_id CLUSTERED);
	INSERT INTO #searchable_forums_table
	SELECT value FROM string_split(@searchable_forums, ',');

	CREATE TABLE #filtered_posts (post_id int INDEX ix_post_id CLUSTERED);

	CREATE TABLE #posts (forum_id int not null, 
						 topic_id int not null, 
						 post_id int UNIQUE not null, 
						 post_subject nvarchar(255) not null, 
						 post_text nvarchar(max) not null, 
						 author_id int not null INDEX ix_author_id CLUSTERED, 
						 bbcode_uid nvarchar(8) not null, 
						 post_time bigint not null,
						 post_edit_count int not null,
						 post_edit_reason nvarchar(255) not null,
						 post_edit_time bigint not null,
						 [ip] nvarchar(40) not null,
						 post_username nvarchar(255) not null,
						 post_edit_user int not null,
						 total_count int not null);

	SET @search_text = ltrim(rtrim(@search_text));
	IF (coalesce(@search_text, '') <> '')
	BEGIN
		WHILE CHARINDEX('  ', @search_text) > 0 
			SET @search_text = REPLACE(@search_text, '  ', ' ');
			
		IF (@search_text NOT LIKE '"%"')
			SET @search_text = REPLACE(@search_text, ' ', ' OR ');

		INSERT INTO #filtered_posts
		SELECT p.post_id
		  FROM phpbb_posts p
		 WHERE CONTAINS(*, @search_text);

		INSERT INTO #posts
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
			   p.poster_ip as ip,
			   p.post_username,
			   p.post_edit_user,
			   count(1) over() as total_count
		  FROM phpbb_posts p
		  JOIN #searchable_forums_table sft ON sft.forum_id = p.forum_id
		  JOIN #filtered_posts fp ON fp.post_id = p.post_id
		 WHERE (@topic_id = 0 OR @topic_id = p.topic_id)
		   AND (@author_id = 0 OR @author_id = p.poster_id)
		 ORDER BY p.post_time DESC
		OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
	END;
	ELSE
	BEGIN
		INSERT INTO #posts
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
			   p.poster_ip as ip,
			   p.post_username,
			   p.post_edit_user,
			   count(1) over() as total_count
		  FROM phpbb_posts p
		  JOIN #searchable_forums_table sft ON sft.forum_id = p.forum_id
		 WHERE (@topic_id = 0 OR @topic_id = p.topic_id)
		   AND (@author_id = 0 OR @author_id = p.poster_id)
		 ORDER BY p.post_time DESC
		OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
	END;

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
		   a.user_colour as author_color,
		   a.user_avatar as author_avatar,
		   e.username as post_edit_user,
		   r.rank_title
	  FROM #posts p
	  LEFT JOIN #ranks r ON p.author_id = r.user_id
	  LEFT JOIN phpbb_users a ON p.author_id = a.user_id
	  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id;

END
