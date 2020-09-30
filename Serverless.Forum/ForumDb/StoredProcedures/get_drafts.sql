CREATE DEFINER=`root`@`localhost` PROCEDURE `get_drafts`(user_id_parm int, skip_parm int, take_parm int, excluded_forums mediumtext)
BEGIN
	SET @user_id = COALESCE (user_id_parm, 1);
    SET @skip = COALESCE (skip_parm, 0);
    SET @take = COALESCE (take_parm, 14);
    
    SET @sql = concat(
		"SELECT  d.draft_id,
				 d.topic_id, 
				 d.forum_id,
				 d.draft_subject as topic_title,
				 d.save_time as topic_last_post_time,
				 t.topic_last_post_id
			FROM forum.phpbb_drafts d
			LEFT JOIN forum.phpbb_topics t
			  ON d.topic_id = t.topic_id
		WHERE NOT FIND_IN_SET(d.forum_id, '", excluded_forums, "')
		  AND d.user_id = ?
		  AND d.forum_id <> 0
          AND (t.topic_id IS NOT NULL OR d.topic_id = 0)
		ORDER BY d.save_time DESC
		LIMIT ?, ?"
    );
    
    PREPARE stmt FROM @sql;
	EXECUTE stmt USING @user_id, @skip, @take;
	DEALLOCATE PREPARE stmt;
    
    SELECT COUNT(*) as total_count
    FROM forum.phpbb_drafts d
	LEFT JOIN forum.phpbb_topics t
	  ON d.topic_id = t.topic_id
	WHERE NOT FIND_IN_SET(d.forum_id, excluded_forums)
      AND d.user_id = @user_id
	  AND d.forum_id <> 0
	  AND (t.topic_id IS NOT NULL OR d.topic_id = 0);
END