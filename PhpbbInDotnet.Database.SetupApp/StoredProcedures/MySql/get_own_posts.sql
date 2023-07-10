CREATE DEFINER=`root`@`localhost` PROCEDURE `get_own_posts`(user_id_parm int, excluded_forums mediumtext, skip_parm int, take_parm int)
BEGIN
	SET @user_id = COALESCE (user_id_parm, 1);
    SET @skip = COALESCE (skip_parm, 0);
    SET @take = COALESCE (take_parm, 14);
    
    SET @total_count = (
		SELECT @total_count = count(DISTINCT p.topic_id)
		  FROM phpbb_posts p
		 WHERE p.poster_id = @user_id AND NOT FIND_IN_SET(p.forum_id, excluded_forums));
    
    SET @sql = concat(
		"WITH own_topics AS (
			SELECT DISTINCT p.topic_id
			FROM phpbb_posts p
			JOIN phpbb_topics t 
			  ON p.topic_id = t.topic_id
			WHERE p.poster_id = ?
              AND NOT FIND_IN_SET (t.forum_id, '", excluded_forums, "')
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
			   t.topic_last_post_id,
               ? AS total_count
		FROM phpbb_posts p
		JOIN own_topics ot
		  ON p.topic_id = ot.topic_id
		JOIN phpbb_topics t
          ON t.topic_id = ot.topic_id
		GROUP BY p.topic_id;"
	);
	
	PREPARE stmt FROM @sql;
	EXECUTE stmt USING @user_id, @skip, @take, @total_count;
	DEALLOCATE PREPARE stmt;

END