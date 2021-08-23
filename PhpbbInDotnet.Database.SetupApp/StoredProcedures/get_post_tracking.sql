CREATE DEFINER=`root`@`localhost` PROCEDURE `get_post_tracking`(user_id_parm int)
BEGIN
	/**
		https://www.phpbb.com/community/viewtopic.php?t=2165146
		https://www.phpbb.com/community/viewtopic.php?p=2987015
	**/
    
	SET @user_id = COALESCE(user_id_parm, 1);
	SELECT user_lastmark
	  INTO @user_lastmark				
      FROM phpbb_users
	 WHERE user_id = @user_id
     LIMIT 1;
	
    WITH marktimes AS (
		SELECT t.topic_id, 
				 t.forum_id,
				 t.topic_last_post_time,
				 tt.mark_time as topic_mark_time, 
				 ft.mark_time as forum_mark_time
		  FROM phpbb_topics t
		  LEFT JOIN phpbb_topics_track tt
				 ON tt.topic_id = t.topic_id 
				AND tt.user_id = @user_id
		  LEFT JOIN phpbb_forums_track ft
				 ON ft.forum_id=t.forum_id 
				AND ft.user_id = @user_id
		  WHERE @user_id <> 1
            AND t.topic_last_post_time > @user_lastmark
            AND ((tt.mark_time IS NOT NULL AND t.topic_last_post_time > tt.mark_time) OR (ft.mark_time IS NOT NULL AND t.topic_last_post_time > ft.mark_time))
	)
	SELECT m.*, @user_lastmark as user_lastmark, group_concat(p.post_id) AS post_ids
	  FROM marktimes m
	  JOIN phpbb_posts p
		ON m.topic_id = p.topic_id
	 WHERE
       p.poster_id <> @user_id
       AND @user_id <> 1
       AND post_time > @user_lastmark
            AND ((topic_mark_time IS NOT NULL AND post_time > topic_mark_time) OR (forum_mark_time IS NOT NULL AND post_time > forum_mark_time))
       GROUP BY m.topic_id;
END