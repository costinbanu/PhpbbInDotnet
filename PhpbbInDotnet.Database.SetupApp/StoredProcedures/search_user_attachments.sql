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
            SELECT p.post_id,
                   p.post_subject,
                   p.post_text,
                   case when p.poster_id = 1
                        then p.post_username 
                        else u.username
                   end as author_name,
                   p.poster_id as author_id,
                   p.bbcode_uid,
                   from_unixtime(p.post_time) as post_creation_time,
                   u.user_colour as author_color,
                   u.user_avatar,
                   u.user_sig,
                   u.user_sig_bbcode_uid,
                   p.post_time,
                   p.forum_id
              FROM phpbb_posts p
              JOIN phpbb_users u
                ON p.poster_id = u.user_id
			  JOIN phpbb_attachments a
                ON p.post_id = a.post_msg_id
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
    FROM attach_search_results;

	WITH search_stmt AS (
		SELECT p.post_id
		  FROM phpbb_posts p
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