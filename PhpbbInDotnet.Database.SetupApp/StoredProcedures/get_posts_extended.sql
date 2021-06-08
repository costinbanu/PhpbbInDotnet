CREATE DEFINER=`root`@`localhost` PROCEDURE `get_posts_extended`(user_id int, topic_id int, page_no int, page_size int, post_id int)
BEGIN
	
    /*  
		sets the @page_no and @topic_id session variables
		populates the get_posts temporary table 
        returns the phpbb_posts within this topic and page
        returns the phpbb_users that are the posts authors
        returns the phpbb_attachments associated with these posts
	*/
    CALL get_posts(user_id, topic_id, page_no, page_size, post_id, 0);
    
    /* page */
	SELECT @page_no AS page_num;
		
	/* count */
	SELECT COUNT(1) AS total_count
	  FROM phpbb_posts p
	WHERE p.topic_id = @topic_id;
      
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

END