CREATE DEFINER=`root`@`localhost` PROCEDURE `get_post_position_in_topic`(topic_id_parm int, post_id_parm int)
BEGIN
	SET @row_num = 0;
	  WITH row_numbers AS (
		  SELECT @row_num := @row_num + 1 AS row_num,
				 post_id
			FROM phpbb_posts
		   WHERE topic_id = topic_id_parm
		   ORDER BY post_time
	  )
	  SELECT row_num
		FROM row_numbers
	   WHERE post_id = post_id_parm;
END