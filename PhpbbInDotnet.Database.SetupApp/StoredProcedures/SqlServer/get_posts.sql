/****** Object:  StoredProcedure [dbo].[get_posts]    Script Date: 03.07.2023 20:40:16 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[get_posts] (@topic_id int, @anonymous_user_id int, @order nvarchar(4), @skip int, @take int, @include_reports bit)
AS
BEGIN
	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL SNAPSHOT;

	DECLARE @total_count int;
	SET @total_count = (
		SELECT count(1) 
		  FROM phpbb_posts 
		 WHERE topic_id = @topic_id);

	DECLARE @posts TABLE(
		forum_id int,
		topic_id int,
		post_id int index ix_posts_post_id,
		post_subject nvarchar(255),
		post_text nvarchar(max),
		author_id int index ix_posts_author_id,
		bbcode_uid nvarchar(8),
		post_time bigint,
		post_edit_count int,
		post_edit_reason nvarchar(255),
		post_edit_time bigint, 
		[ip] nvarchar(40),
		post_username nvarchar(255),
		post_edit_user int);

	IF (@order = 'DESC')
		WITH post_ids AS (
			SELECT post_id
			  FROM phpbb_posts
			 WHERE topic_id = @topic_id 
			 ORDER BY post_time DESC
			OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
		)
		INSERT INTO @posts
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
		  FROM phpbb_posts p
		  JOIN post_ids pid ON p.post_id = pid.post_id;
	ELSE
		WITH post_ids AS (
			SELECT post_id
			  FROM phpbb_posts
			 WHERE topic_id = @topic_id 
			 ORDER BY post_time ASC
			OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
		)
		INSERT INTO @posts
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
		  FROM phpbb_posts p
		  JOIN post_ids pid ON p.post_id = pid.post_id;

	DECLARE @ranks TABLE(
		user_id int index ix_ranks_user_id,
		rank_id int index ix_ranks_rank_id,
		rank_title nvarchar(255));

	INSERT INTO @ranks
	SELECT DISTINCT u.user_id, 
		   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
		   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
	  FROM phpbb_users u
	  JOIN phpbb_groups g ON u.group_id = g.group_id
	  JOIN @posts p on p.author_id = u.user_id
	  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
	  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id

	IF (@order = 'DESC')
		SELECT p.*, 
			   CASE WHEN p.author_id = @anonymous_user_id THEN p.post_username ELSE a.username END AS author_name,
			   a.user_colour AS author_color,
			   a.user_avatar AS author_avatar,
			   e.username AS post_edit_user,
			   r.rank_title,
			   @total_count AS total_count
		  FROM @posts p
		  LEFT JOIN @ranks r ON p.author_id = r.user_id
		  LEFT JOIN phpbb_users a ON p.author_id = a.user_id
		  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
		 ORDER BY post_time DESC;
	ELSE
		SELECT p.*, 
			   CASE WHEN p.author_id = @anonymous_user_id THEN p.post_username ELSE a.username END AS author_name,
			   a.user_colour AS author_color,
			   a.user_avatar AS author_avatar,
			   e.username AS post_edit_user,
			   r.rank_title,
			   @total_count AS total_count
		  FROM @posts p
		  LEFT JOIN @ranks r ON p.author_id = r.user_id
		  LEFT JOIN phpbb_users a ON p.author_id = a.user_id
		  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
		 ORDER BY post_time ASC;

	SELECT a.*
	  FROM phpbb_attachments a
	  JOIN @posts p ON a.post_msg_id = p.post_id;

	IF (@include_reports = 1)
		SELECT r.report_id AS id, 
			   rr.reason_title, 
               rr.reason_description, 
               r.report_text AS details, 
               r.user_id AS reporter_id, 
               u.username AS reporter_username, 
               r.post_id,
               r.report_time,
               r.report_closed
		  FROM phpbb_reports r
		  JOIN phpbb_reports_reasons rr ON r.reason_id = rr.reason_id
		  JOIN phpbb_users u ON r.user_id = u.user_id
		  JOIN @posts p ON r.post_id = p.post_id;

END
