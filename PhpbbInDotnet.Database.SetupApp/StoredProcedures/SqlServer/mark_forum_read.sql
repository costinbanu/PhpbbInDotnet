CREATE PROCEDURE [dbo].[mark_forum_read](@forum_id int, @user_id int, @mark_time bigint)
AS
BEGIN
    SET NOCOUNT ON;

	DELETE 
	  FROM phpbb_topics_track 
	 WHERE forum_id = @forum_id AND user_id = @user_id;

	 UPDATE phpbb_forums_track
	    SET  mark_time = @mark_time
	  WHERE forum_id = @forum_id AND user_id = @user_id;

	 IF @@ROWCOUNT = 0
		INSERT INTO phpbb_forums_track (forum_id, user_id, mark_time) 
		VALUES (@forum_id, @user_id, @mark_time);
END
;

