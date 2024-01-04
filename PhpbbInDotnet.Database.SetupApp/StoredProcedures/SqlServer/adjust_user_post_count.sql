SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[adjust_user_post_count] (@post_ids nvarchar(max), @post_operation nvarchar(6))
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @factor int;

	IF @post_operation = 'add'
		SET @factor = 1;
	ELSE IF @post_operation = 'delete'
		SET @factor = -1;
	ELSE 
	BEGIN
		RAISERROR('Unexpected value ''%s'' for parameter @post_operation, expected either of ''%s'', ''%s''.', 18, 1, @post_operation, 'add', 'delete');
		RETURN;
	END;

	CREATE TABLE #post_ids (post_id int INDEX ix_post_id CLUSTERED);
	INSERT INTO #post_ids
	SELECT value FROM string_split(@post_ids, ',');

	WITH counts AS (
		SELECT p.poster_id, count(p.post_id) as post_count
		  FROM phpbb_posts p
		  JOIN #post_ids pi on p.post_id = pi.post_id
		 GROUP BY p.poster_id
	)
	UPDATE u
	   SET u.user_posts = u.user_posts + c.post_count * @factor
	  FROM phpbb_users u
	  JOIN counts c ON u.user_id = c.poster_id

END
