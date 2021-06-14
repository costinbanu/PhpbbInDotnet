CREATE DEFINER=`root`@`localhost` PROCEDURE `get_posts`(user_id int, topic_id int, page_no int, page_size int, post_id int, for_posting tinyint)
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
    
	SET @topic_id = topic_id;
    
	DROP TEMPORARY TABLE IF EXISTS get_posts;
    
    IF for_posting = 0
    THEN
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

		SET @start_idx = (page_no - 1) * @page_size;
			
		PREPARE stmt FROM 
			"CREATE TEMPORARY TABLE get_posts 
				WITH ranks AS (
					SELECT DISTINCT u.user_id, 
						   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
						   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
					  FROM phpbb_users u
					  JOIN phpbb_groups g ON u.group_id = g.group_id
					  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
					  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
				)
				SELECT 
					   p.forum_id,
					   p.topic_id,
					   p.post_id,
					   p.post_subject,
					   p.post_text,
					   case when p.poster_id = 1
							then p.post_username 
							else a.username
					   end as author_name,
					   p.poster_id as author_id,
					   p.bbcode_uid,
					   p.post_time,
					   a.user_colour as author_color,
					   a.user_avatar as author_avatar,
					   p.post_edit_count,
					   p.post_edit_reason,
					   p.post_edit_time,
					   e.username as post_edit_user,
					   r.rank_title as author_rank,
					   p.poster_ip as ip
				  FROM phpbb_posts p
				  JOIN phpbb_users a ON p.poster_id = a.user_id
				  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
				  LEFT JOIN ranks r ON a.user_id = r.user_id
				  WHERE topic_id = ? 
				  ORDER BY post_time 
				  LIMIT ?, ?;";
		EXECUTE stmt USING @topic_id, @start_idx, @page_size;
		DEALLOCATE PREPARE stmt;
	ELSE
		CREATE TEMPORARY TABLE get_posts 
			WITH ranks AS (
					SELECT DISTINCT u.user_id, 
						   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
						   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
					  FROM phpbb_users u
					  JOIN phpbb_groups g ON u.group_id = g.group_id
					  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
					  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
			)
			SELECT 
				   p.forum_id,
				   p.topic_id,
				   p.post_id,
				   p.post_subject,
				   p.post_text,
				   case when p.poster_id = 1
						then p.post_username 
						else a.username
				   end as author_name,
				   p.poster_id as author_id,
				   p.bbcode_uid,
				   p.post_time,
				   a.user_colour as author_color,
				   a.user_avatar as author_avatar,
				   p.post_edit_count,
				   p.post_edit_reason,
				   p.post_edit_time,
				   e.username as post_edit_user,
				   r.rank_title as author_rank,
				   p.poster_ip as ip
			  FROM phpbb_posts p
			  JOIN phpbb_users a ON p.poster_id = a.user_id
			  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
			  LEFT JOIN ranks r ON a.user_id = r.user_id
			 WHERE p.topic_id = topic_id
			 ORDER BY p.post_time DESC
			 LIMIT 14;
	END IF;
    
	SET @page_no = page_no;
	
    /* posts */
    SELECT *
      FROM get_posts
      ORDER BY 
		CASE WHEN for_posting = 0 THEN post_time END ASC,
        CASE WHEN for_posting = 1 THEN post_time END DESC;
      
	/* attachments */
    SELECT a.*
      FROM phpbb_attachments a
      JOIN get_posts p ON a.post_msg_id = p.post_id
	 ORDER BY attach_id;
    
END