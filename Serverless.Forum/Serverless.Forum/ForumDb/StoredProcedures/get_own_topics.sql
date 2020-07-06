CREATE DEFINER=`root`@`localhost` PROCEDURE `get_own_topics`(user_id_parm int, skip_parm int, take_parm int)
BEGIN
	SET @user_id = COALESCE (user_id_parm, 1);
    SET @skip = COALESCE (skip_parm, 0);
    SET @take = COALESCE (take_parm, 14);
    
    SET @sql = "
		WITH own_topics AS (
			SELECT DISTINCT p.topic_id
			FROM phpbb_posts p
			JOIN phpbb_topics t 
			  ON p.topic_id = t.topic_id
			WHERE p.poster_id = ?
			ORDER BY t.topic_last_post_time DESC
			LIMIT ?, ?
		)
		SELECT t.topic_id, 
			   t.forum_id,
			   t.topic_title, 
			   count(p.post_id) AS post_count,
			   t.topic_views AS view_count,
			   t.topic_type,
			   t.topic_last_poster_id,
			   t.topic_last_poster_name,
			   t.topic_last_post_time,
			   t.topic_last_poster_colour,
			   t.topic_last_post_id
		FROM phpbb_posts p
		JOIN own_topics ot
		  ON p.topic_id = ot.topic_id
		JOIN phpbb_topics t
          ON t.topic_id = ot.topic_id
		GROUP BY p.topic_id;";
	
	PREPARE stmt FROM @sql;
	EXECUTE stmt USING @user_id, @skip, @take;
	DEALLOCATE PREPARE stmt;
    
    SELECT COUNT(DISTINCT topic_id) AS total_count 
	FROM phpbb_posts 
	WHERE poster_id = @user_id;
END