CREATE DEFINER=`root`@`localhost` PROCEDURE `get_post_tracking`(user_id_parm int, topic_id_parm int, forum_id_parm int)
BEGIN
    IF user_id_parm IS NULL /*OR user_id_parm = 1*/ THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'NULL or ''1'' is not allowed for user_id.';
    END IF;
    
    /**
		https://www.phpbb.com/community/viewtopic.php?t=2165146
		https://www.phpbb.com/community/viewtopic.php?p=2987015
	**/
    WITH marktimes AS (
		SELECT t.topic_id, 
				 t.forum_id,
				 t.topic_last_post_time,
				 tt.mark_time as topic_mark_time, 
				 ft.mark_time as forum_mark_time, 
				 u.user_lastmark
		  FROM phpbb_users u, 
			   phpbb_topics t
		  LEFT JOIN phpbb_topics_track tt
				 ON tt.topic_id = t.topic_id 
				AND tt.user_id = user_id_parm
		  LEFT JOIN phpbb_forums_track ft
				 ON ft.forum_id=t.forum_id 
				AND ft.user_id = user_id_parm
		  WHERE u.user_id = user_id_parm 
			AND topic_last_post_time > u.user_lastmark
            AND (ISNULL(topic_id_parm) OR (t.topic_id = topic_id_parm))
            AND (ISNULL(forum_id_parm) OR (t.forum_id = forum_id_parm))
	)
	SELECT m.* , p.post_id
	  FROM marktimes m
	  JOIN phpbb_posts p
		ON m.topic_id = p.topic_id
	   AND (1 AND
		   NOT (
				(p.post_time <= topic_mark_time AND NOT ISNULL(topic_mark_time)) OR 
				(p.post_time <= forum_mark_time AND NOT ISNULL(forum_mark_time))
		   )
	   )
	 WHERE 1 AND
	   NOT (
			(topic_last_post_time <= topic_mark_time AND NOT ISNULL(topic_mark_time)) OR 
			(topic_last_post_time <= forum_mark_time AND NOT ISNULL(forum_mark_time))
	   ) AND
       p.poster_id <> user_id_parm;
END