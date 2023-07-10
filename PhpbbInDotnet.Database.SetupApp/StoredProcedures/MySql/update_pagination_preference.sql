CREATE DEFINER=`root`@`localhost` PROCEDURE `update_pagination_preference`(user_id_parm int, topic_id_parm int, posts_per_page int)
BEGIN
	UPDATE phpbb_user_topic_post_number 
	   SET post_no = posts_per_page
	 WHERE user_id = user_id_parm AND topic_id = topic_id_parm;

	IF (ROW_COUNT() = 0) THEN
	   INSERT INTO phpbb_user_topic_post_number (user_id, topic_id, post_no) 
       VALUES (user_id_parm, topic_id_parm, posts_per_page);
	END IF;
END