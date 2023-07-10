CREATE DEFINER=`root`@`localhost` PROCEDURE `get_new_posts`(allowed_topics mediumtext, restricted_forums mediumtext, skip int, take int)
BEGIN
	SET @sql = concat(
		"SELECT t.topic_id, 
			   t.forum_id,
			   t.topic_title, 
			   count(p.post_id) AS post_count,
			   t.topic_views AS view_count,
			   t.topic_last_poster_id,
			   t.topic_last_poster_name,
			   t.topic_last_post_time,
			   t.topic_last_poster_colour,
			   t.topic_last_post_id
		  FROM phpbb_topics t
		  JOIN phpbb_posts p ON t.topic_id = p.topic_id
		 WHERE NOT FIND_IN_SET(t.forum_id,'", restricted_forums, "') AND FIND_IN_SET(t.topic_id,'", allowed_topics, "')
		 GROUP BY t.topic_id, t.topic_title, t.forum_id, t.topic_views, t.topic_last_poster_id, t.topic_last_poster_name, t.topic_last_post_time, t.topic_last_poster_colour, t.topic_last_post_id
		 ORDER BY t.topic_last_post_time DESC
		 LIMIT ?, ?;");
         
	SET @skip_var = skip;
    SET @take_var = take;
	PREPARE stmt FROM @sql;
	EXECUTE stmt USING @skip_var, @take_var;
	DEALLOCATE PREPARE stmt;
END