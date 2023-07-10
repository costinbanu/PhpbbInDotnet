CREATE DEFINER=`root`@`localhost` PROCEDURE `sync_post_forum`()
BEGIN
    UPDATE phpbb_posts p
      JOIN phpbb_topics t ON p.topic_id = t.topic_id
	   SET p.forum_id = t.forum_id
	 WHERE p.forum_id <> t.forum_id;
END