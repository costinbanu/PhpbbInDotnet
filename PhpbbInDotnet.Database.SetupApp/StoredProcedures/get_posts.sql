CREATE DEFINER=`root`@`localhost` PROCEDURE `get_posts`(user_id int, topic_id int, page_no int, page_size int, post_id int)
BEGIN
  
	IF topic_id IS NULL AND post_id IS NULL
    THEN 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'topic_id and post_id can''t be both null', MYSQL_ERRNO = 1001;
	ELSEIF topic_id IS NULL
    THEN
		SELECT p.topic_id
          INTO topic_id
          FROM phpbb_posts p
         WHERE p.post_id = post_id
         LIMIT 1;
	END IF;
    
    IF page_size IS NULL
    THEN
		SET @page_size = 14;
		SELECT utpn.post_no
		  INTO @page_size 
		  FROM phpbb_user_topic_post_number utpn
		 WHERE utpn.user_id = user_id
		   AND utpn.topic_id = topic_id
		 LIMIT 1;
	ELSE
		SET @page_size = page_size;
    END IF;

    IF page_no IS NULL
    THEN
		IF post_id IS NULL
        THEN
			SELECT p.post_id
              INTO post_id
              FROM phpbb_posts p
			 WHERE p.topic_id = topic_id
			 ORDER BY p.post_time DESC
			 LIMIT 1;
		  END IF;
 
		SET @rownum = 0;
        SET @idx = 1;
        
		SELECT x.position
		  INTO @idx 
          FROM (
			SELECT p.post_id, 
				   @rownum:=@rownum + 1 AS position
			  FROM phpbb_posts p
			  JOIN (
				SELECT @rownum:=0
			  ) r
			 WHERE p.topic_id = topic_id
			 ORDER BY p.post_time
          ) x
		 WHERE x.post_id = post_id
		 LIMIT 1;

	  SET page_no = @idx DIV @page_size;

	  IF @idx MOD @page_size <> 0
	  THEN SET page_no = page_no + 1;
	  END IF;
    END IF;

    SET @topic_id = topic_id;
	SET @page_no = page_no;
	SET @tid = topic_id;
	SET @start_idx = (@page_no - 1) * @page_size;
    
    DROP TEMPORARY TABLE IF EXISTS get_posts;
    
	PREPARE stmt FROM 
		"CREATE TEMPORARY TABLE get_posts 
         SELECT * 
           FROM phpbb_posts 
		  WHERE topic_id = ? 
          ORDER BY post_time 
          LIMIT ?, ?;";
	EXECUTE stmt USING @tid, @start_idx, @page_size;
	DEALLOCATE PREPARE stmt;
	
    /* posts */
    SELECT *
      FROM get_posts;
    
    /* authors */
    SELECT DISTINCT u.*
      FROM phpbb_users u
      JOIN get_posts p ON u.user_id = p.poster_id;
      
	/* attachments */
    SELECT a.*
      FROM phpbb_attachments a
      JOIN get_posts p ON a.post_msg_id = p.post_id
	 ORDER BY attach_id;
    
END