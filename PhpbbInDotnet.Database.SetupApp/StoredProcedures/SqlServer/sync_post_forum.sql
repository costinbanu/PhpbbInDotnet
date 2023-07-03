/****** Object:  StoredProcedure [dbo].[sync_post_forum]    Script Date: 03.07.2023 20:45:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[sync_post_forum]
AS
BEGIN
    SET NOCOUNT ON

    UPDATE p
	   SET p.forum_id = t.forum_id
	  FROM phpbb_posts p
      JOIN phpbb_topics t ON p.topic_id = t.topic_id
	 WHERE p.forum_id <> t.forum_id;

END
GO

