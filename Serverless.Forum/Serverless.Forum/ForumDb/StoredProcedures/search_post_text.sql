CREATE DEFINER=`root`@`localhost` PROCEDURE `search_post_text`(forum int, topic int, author int, page_no int, excluded_forums mediumtext, search mediumtext)
BEGIN
    if page_no is null
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'page_no can''t be null', MYSQL_ERRNO = 1001;
	end if;
    
    if excluded_forums is null
    then
		set excluded_forums = '0';
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
				p.post_time
		   FROM phpbb_posts p
		   JOIN phpbb_users u
			ON p.poster_id = u.user_id
		  WHERE (? IS NULL OR ? = p.forum_id)
			AND (? IS NULL OR ? = p.topic_id)
			AND (? IS NULL OR ? = p.poster_id)
			AND (? IS NULL OR MATCH(p.post_text) AGAINST(? IN BOOLEAN MODE) OR MATCH(p.post_subject) AGAINST(? IN BOOLEAN MODE))
			AND p.forum_id NOT IN (", excluded_forums, ")	
		  ORDER BY p.post_time DESC
	  LIMIT ?, 14;"
    );
    set @start_idx = (page_no - 1) * 14;
	PREPARE stmt FROM @sql;
          
    set @forum = forum;
    set @topic = topic;
    set @author = author;
    set @search = search;
    
	EXECUTE stmt USING @forum, @forum, @topic, @topic, @author, @author, @search, @search, @search, @start_idx;
	DEALLOCATE PREPARE stmt;

	SELECT count(1) as total_count
      FROM phpbb_posts p
      JOIN phpbb_topics t
        ON p.topic_id = t.topic_id
     WHERE (forum IS NULL OR forum = t.forum_id)
       AND (topic IS NULL OR topic = p.topic_id)
       AND (author IS NULL OR author = p.poster_id)
       AND (search IS NULL OR MATCH(p.post_text) AGAINST(search IN BOOLEAN MODE) OR MATCH(p.post_subject) AGAINST(search IN BOOLEAN MODE));

END