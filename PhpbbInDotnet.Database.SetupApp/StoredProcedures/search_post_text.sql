CREATE DEFINER=`root`@`localhost` PROCEDURE `search_post_text`(forums mediumtext, topic int, author int, page_no int, search mediumtext)
BEGIN
    if coalesce(page_no, 0) <= 0
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'page_no can''t be null or less than 1', MYSQL_ERRNO = 1001;
	end if;
    
    DROP TEMPORARY TABLE IF EXISTS post_text_search_results;
    
    set @sql = CONCAT (
        "CREATE TEMPORARY TABLE post_text_search_results
			WITH ranks AS (
				SELECT DISTINCT u.user_id, 
					   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
					   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
				  FROM phpbb_users u
				  JOIN phpbb_groups g ON u.group_id = g.group_id
				  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
				  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
			)
			SELECT *
            FROM (
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
				  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
				  LEFT JOIN ranks r ON a.user_id = r.user_id
				 WHERE FIND_IN_SET (p.forum_id, '", forums, "')
				   AND (? IS NULL OR ? = p.topic_id)
				   AND (? IS NULL OR ? = p.poster_id)
				   AND (? IS NULL OR MATCH(p.post_text) AGAINST(? IN BOOLEAN MODE))
				  
				 UNION
				  
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
				  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
				  LEFT JOIN ranks r ON a.user_id = r.user_id
				 WHERE FIND_IN_SET (p.forum_id, '", forums, "')
				   AND (? IS NULL OR ? = p.topic_id)
				   AND (? IS NULL OR ? = p.poster_id)
				   AND (? IS NULL OR MATCH(p.post_subject) AGAINST(? IN BOOLEAN MODE))
                   
				 ORDER BY post_time DESC
				 LIMIT ?, 14
			) a;"
    );
    
    set @start_idx = (page_no - 1) * 14;
	PREPARE stmt FROM @sql;
          
    set @topic = topic;
    set @author = author;
    set @search = search;
    
	EXECUTE stmt USING @topic, @topic, @author, @author, @search, @search, @topic, @topic, @author, @author, @search, @search, @start_idx;
	DEALLOCATE PREPARE stmt;
    
    SELECT *
      FROM post_text_search_results
     ORDER BY post_time DESC;

	WITH search_stmt AS (
		SELECT p.post_id
		  FROM phpbb_posts p
	     WHERE FIND_IN_SET (p.forum_id, forums)
		   AND (topic IS NULL OR topic = p.topic_id)
		   AND (author IS NULL OR author = p.poster_id)
		   AND (search IS NULL OR MATCH(p.post_text) AGAINST(search IN BOOLEAN MODE))
		 
         UNION            
		
		SELECT p.post_id
		  FROM phpbb_posts p
		 WHERE FIND_IN_SET (p.forum_id, forums)
		   AND (topic IS NULL OR topic = p.topic_id)
		   AND (author IS NULL OR author = p.poster_id)
		   AND (search IS NULL OR MATCH(p.post_subject) AGAINST(search IN BOOLEAN MODE))
    )
    
	SELECT count(1) as total_count
      FROM search_stmt;
      
	SELECT a.*
      FROM phpbb_attachments a
	  JOIN post_text_search_results t 
        ON a.post_msg_id = t.post_id;
END