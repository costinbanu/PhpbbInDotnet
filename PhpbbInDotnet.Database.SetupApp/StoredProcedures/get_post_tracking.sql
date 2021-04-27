CREATE DEFINER=`root`@`localhost` PROCEDURE `get_post_tracking`(user_id_parm int)
BEGIN
	SET @user_id = COALESCE(user_id_parm, 1);

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
				AND tt.user_id = @user_id
		  LEFT JOIN phpbb_forums_track ft
				 ON ft.forum_id=t.forum_id 
				AND ft.user_id = @user_id
		  WHERE u.user_id = @user_id 
		    AND @user_id <> 1
            AND t.topic_last_post_time > coalesce(tt.mark_time, ft.mark_time, u.user_lastmark, 0)
	)
	SELECT m.* , group_concat(p.post_id) AS post_ids
	  FROM marktimes m
	  JOIN phpbb_posts p
		ON m.topic_id = p.topic_id
	   AND p.post_time > coalesce(topic_mark_time, forum_mark_time, user_lastmark, 0)
	 WHERE p.poster_id <> @user_id
       AND @user_id <> 1
       GROUP BY m.topic_id;
END