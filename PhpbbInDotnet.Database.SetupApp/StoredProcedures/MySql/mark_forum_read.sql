CREATE DEFINER=`root`@`localhost` PROCEDURE `mark_forum_read`(forum_id_parm int, user_id_parm int, mark_time_parm bigint)
BEGIN
	DELETE 
	  FROM phpbb_topics_track 
	 WHERE forum_id = forum_id_parm AND user_id = user_id_parm;

	 UPDATE phpbb_forums_track
	    SET  mark_time = mark_time_parm
	  WHERE forum_id = forum_id_parm AND user_id = user_id_parm;

	 IF (ROW_COUNT() = 0) THEN
		INSERT INTO phpbb_forums_track (forum_id, user_id, mark_time) 
		VALUES (forum_id_parm, user_id_parm, mark_time_parm);
     END IF;   
END