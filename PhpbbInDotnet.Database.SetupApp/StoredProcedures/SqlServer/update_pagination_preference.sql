/****** Object:  StoredProcedure [dbo].[update_pagination_preference]    Script Date: 03.07.2023 20:46:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[update_pagination_preference](@user_id int, @topic_id int, @posts_per_page int)
AS
BEGIN
    SET NOCOUNT ON;

	UPDATE phpbb_user_topic_post_number 
	   SET post_no = @posts_per_page
	 WHERE [user_id] = @user_id AND topic_id = @topic_id;

	IF @@ROWCOUNT = 0
	   INSERT INTO phpbb_user_topic_post_number ([user_id], topic_id, post_no) 
       VALUES (@user_id, @topic_id, @posts_per_page);
END
GO

