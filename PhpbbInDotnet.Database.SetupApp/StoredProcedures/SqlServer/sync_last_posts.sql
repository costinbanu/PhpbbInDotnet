/****** Object:  StoredProcedure [dbo].[sync_last_posts]    Script Date: 03.07.2023 20:44:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[sync_last_posts] (@anonymous_user_id int, @anonymous_username nvarchar(255), @default_user_colour nvarchar(6))
AS
BEGIN
    SET NOCOUNT ON;

	EXECUTE [dbo].[sync_forum_with_posts] 
	   null
	  ,@anonymous_user_id
	  ,@anonymous_username
	  ,@default_user_colour;

	EXECUTE [dbo].[sync_topic_with_posts] 
	   null
	  ,@anonymous_user_id
	  ,@anonymous_username
	  ,@default_user_colour;


END
