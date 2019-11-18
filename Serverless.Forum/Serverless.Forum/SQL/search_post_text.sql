CREATE DEFINER=`root`@`localhost` PROCEDURE `search_post_text`(forum int, topic int, author int, page_no int, search mediumtext)
BEGIN
    
    if page_no is null
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'page_no can''t be null', MYSQL_ERRNO = 1001;
	end if;
	
    set @start_idx = (page_no - 1) * 14;
	PREPARE stmt FROM 
		"SELECT p.*
		   FROM phpbb_posts p
		   JOIN phpbb_topics t
		     ON p.topic_id = t.topic_id
		  WHERE (? IS NULL OR ? = t.forum_id)
		    AND (? IS NULL OR ? = p.topic_id)
		    AND (? IS NULL OR ? = p.poster_id)
		    AND (? IS NULL OR MATCH(p.post_text) AGAINST(?))
		  ORDER BY p.post_time
		  LIMIT ?, 14;";
          
    set @forum = forum;
    set @topic = topic;
    set @author = author;
    set @search = search;
    
	EXECUTE stmt USING @forum, @forum, @topic, @topic, @author, @author, @search, @search, @start_idx;
	DEALLOCATE PREPARE stmt;
	
    select page_no;
    
    select count(1) as total_count
     FROM phpbb_posts p
    JOIN phpbb_topics t
      ON p.topic_id = t.topic_id
    WHERE (forum IS NULL OR forum = t.forum_id)
      AND (topic IS NULL OR topic = p.topic_id)
      AND (author IS NULL OR author = p.poster_id)
      AND (search IS NULL OR MATCH(p.post_text) AGAINST(search));

END