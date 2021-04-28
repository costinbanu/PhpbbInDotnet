CREATE DEFINER=`root`@`localhost` PROCEDURE `get_posts_extended`(user_id int, topic_id int, page_no int, page_size int, post_id int)
BEGIN
	
    /*  
		sets the @page_no and @topic_id session variables
		populates the get_posts temporary table 
        returns the phpbb_posts within this topic and page
        returns the phpbb_users that are the posts authors
        returns the phpbb_attachments associated with these posts
	*/
    CALL get_posts(user_id, topic_id, page_no, page_size, post_id);
    
    /* page */
	SELECT CAST(@page_no AS SIGNED INT) AS page_num;
		
	/* count */
	SELECT COUNT(1) AS total_count
	  FROM phpbb_posts p
	WHERE p.topic_id = @topic_id;
      
	/* last edit users */
	SELECT DISTINCT u.*
      FROM phpbb_users u
      JOIN get_posts p ON u.user_id = p.post_edit_user;
      
	/* reports */
    SELECT r.report_id AS id, 
		   rr.reason_title, 
           rr.reason_description, 
           r.report_text AS details, 
           r.user_id AS reporter_id, 
           u.username AS reporter_username, 
           r.post_id 
	  FROM phpbb_reports r
      JOIN phpbb_reports_reasons rr ON r.reason_id = rr.reason_id
      JOIN phpbb_users u on r.user_id = u.user_id
      JOIN get_posts p ON r.post_id = p.post_id
	 WHERE report_closed = 0;
     
     /* ranks */
	SELECT DISTINCT u.user_id, 
		   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
           COALESCE(r1.rank_title, r2.rank_title) AS rank_title
	  FROM phpbb_users u
	  JOIN phpbb_groups g ON u.group_id = g.group_id
      JOIN get_posts p on u.user_id = p.poster_id
	  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
	  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id;
END