CREATE DEFINER=`root`@`localhost` PROCEDURE `search_user_attachments`(restrictedForums mediumtext, user_id int, page_no int)
BEGIN
	if coalesce(page_no, 0) <= 0
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'page_no can''t be null or less than 1', MYSQL_ERRNO = 1001;
	end if;
    
	if coalesce(user_id, 0) <= 1
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'user_id can''t be null or less than 2', MYSQL_ERRNO = 1001;
	end if;
    
    DROP TEMPORARY TABLE IF EXISTS attach_search_results;
    
     set @sql = CONCAT (
        "CREATE TEMPORARY TABLE attach_search_results AS (
            WITH ranks AS (
				SELECT DISTINCT u.user_id, 
					   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
					   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
				  FROM phpbb_users u
				  JOIN phpbb_groups g ON u.group_id = g.group_id
				  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
				  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
			)
			SELECT p.forum_id,
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
			  JOIN phpbb_attachments attach ON p.post_id = attach.post_msg_id
			  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
			  LEFT JOIN ranks r ON a.user_id = r.user_id
			 WHERE NOT FIND_IN_SET (p.forum_id, '", coalesce(restrictedForums, ''), "')
			   AND ? = p.poster_id
			ORDER BY post_time DESC
			LIMIT ?, 14
        );"
    );
    
    set @start_idx = (page_no - 1) * 14;
	PREPARE stmt FROM @sql;
          
    set @author = user_id;
    
	EXECUTE stmt USING @author, @start_idx;
	DEALLOCATE PREPARE stmt;
    
    SELECT *
      FROM attach_search_results
     ORDER BY post_time DESC;

	WITH search_stmt AS (
		SELECT p.post_id
		  FROM phpbb_posts p
          JOIN phpbb_attachments attach ON p.post_id = attach.post_msg_id
	     WHERE NOT FIND_IN_SET (p.forum_id, restrictedForums)
		   AND @author = p.poster_id
    )
    
	SELECT count(1) as total_count
      FROM search_stmt;
      
	SELECT a.*
      FROM phpbb_attachments a
	  JOIN attach_search_results t 
        ON a.post_msg_id = t.post_id;
END