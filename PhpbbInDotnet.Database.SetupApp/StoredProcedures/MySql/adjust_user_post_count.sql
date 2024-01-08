CREATE PROCEDURE `adjust_user_post_count`(post_ids text, post_operation nvarchar(6))
BEGIN
	IF post_operation = 'add' THEN SET @factor = 1;
	ELSE 
		IF post_operation = 'delete' THEN SET @factor = -1;
		ELSE 
			SET @message = CONCAT('Unexpected value ', post_operation, ' for parameter @post_operation, expected either of ''add'', ''delete''.');
			SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @message;
		END IF;
	END IF;
    
    DROP TEMPORARY TABLE IF EXISTS post_ids_tmp;
    CREATE TEMPORARY TABLE post_ids_tmp (INDEX (post_id)) AS
	SELECT post_id
      FROM phpbb_posts
	 WHERE FIND_IN_SET(post_id, post_ids);
    
	DROP TEMPORARY TABLE IF EXISTS counts;
	CREATE TEMPORARY TABLE counts (INDEX (poster_id)) AS
	SELECT p.poster_id, count(p.post_id) as post_count
	  FROM phpbb_posts p
      JOIN post_ids_tmp pit ON p.post_id = pit.post_id
	 GROUP BY p.poster_id;
    
	UPDATE phpbb_users u
	  JOIN counts c ON u.user_id = c.poster_id
       SET u.user_posts = u.user_posts + c.post_count * @factor;

END