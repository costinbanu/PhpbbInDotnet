CREATE DEFINER=`root`@`localhost` PROCEDURE `search_post_text`(forum int, topic int, author int, search mediumtext, fromDate int, toDate int)
BEGIN
	SELECT p.*
    FROM phpbb_posts p
    JOIN phpbb_topics t
      ON p.topic_id = t.topic_id
    WHERE (forum IS NULL OR forum = t.forum_id)
      AND (topic IS NULL OR topic = p.topic_id)
      AND (author IS NULL OR author = p.poster_id)
      AND (fromDate IS NULL OR p.post_time >= fromDate)
      AND (toDate IS NULL OR p.post_time <= toDate)
      AND (search IS NULL OR MATCH(p.post_text) AGAINST(search));
END