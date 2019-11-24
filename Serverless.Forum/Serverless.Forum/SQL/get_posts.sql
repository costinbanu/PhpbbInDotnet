CREATE DEFINER=`root`@`localhost` PROCEDURE `get_posts`(user_id int, topic_id int, page_no int, post_id int)
BEGIN
	set @page_size = 14;
	select utpn.post_no
    into @page_size
    from phpbb_user_topic_post_number utpn
    where utpn.user_id = user_id and utpn.topic_id = topic_id;
    
	if topic_id is null and post_id is null
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'topic_id and post_id can''t be both null', MYSQL_ERRNO = 1001;
	elseif topic_id is null
    then
		select p.topic_id
        into topic_id
        from phpbb_posts p
        where p.post_id = post_id;
	end if;
    
    if page_no is null and post_id is null
    then 
		SIGNAL SQLSTATE '45000'
		SET MESSAGE_TEXT = 'page_no and post_id can''t be both null', MYSQL_ERRNO = 1001;
	elseif page_no is null
    then
		set @rownum = 0;
        set @idx = 1;
        SELECT x.position
          INTO @idx
          FROM (
			SELECT p.post_id,
                   @rownum := @rownum + 1 AS position
              FROM phpbb_posts p
              JOIN (SELECT @rownum := 0) r
			 WHERE p.topic_id = topic_id
             ORDER BY p.post_time) x
          WHERE x.post_id = post_id;
          
          set page_no = @idx / @page_size;
          if @idx mod @page_size <> 0
          then set page_no = page_no + 1;
          end if;
    end if;

	set @tid = topic_id;
	set @start_idx = (page_no - 1) * @page_size;
	PREPARE stmt FROM "select p.* from phpbb_posts p where p.topic_id = ? order by p.post_time limit ?, ?;";
	EXECUTE stmt USING @tid, @start_idx, @page_size;
	DEALLOCATE PREPARE stmt;
	
    select page_no;
    
    select count(1) as total_count
    from phpbb_posts p
    where p.topic_id = topic_id;
    
END