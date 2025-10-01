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
;

