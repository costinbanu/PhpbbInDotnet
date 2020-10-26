CREATE DEFINER=`root`@`localhost` PROCEDURE `search_post_text`(forums mediumtext, topic int, author int, page_no int, search mediumtext)
BEGIN
    if page_no is null
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'page_no can''t be null', MYSQL_ERRNO = 1001;
	end if;
    
	set @sql = CONCAT (
		"SELECT p.post_id,
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
		  WHERE FIND_IN_SET (p.forum_id, '", forums, "')
			AND (? IS NULL OR ? = p.topic_id)
			AND (? IS NULL OR ? = p.poster_id)
			AND (? IS NULL OR MATCH(p.post_text) AGAINST(? IN BOOLEAN MODE))
		  
          UNION
		  
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
		  WHERE FIND_IN_SET (p.forum_id, '", forums, "')
			AND (? IS NULL OR ? = p.topic_id)
			AND (? IS NULL OR ? = p.poster_id)
			AND (? IS NULL OR MATCH(p.post_subject) AGAINST(? IN BOOLEAN MODE))
	  ORDER BY post_time DESC
	  LIMIT ?, 14;"
    );
    
    set @start_idx = (page_no - 1) * 14;
	PREPARE stmt FROM @sql;
          
    set @topic = topic;
    set @author = author;
    set @search = search;
    
	EXECUTE stmt USING @topic, @topic, @author, @author, @search, @search, @topic, @topic, @author, @author, @search, @search, @start_idx;
	DEALLOCATE PREPARE stmt;

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

END