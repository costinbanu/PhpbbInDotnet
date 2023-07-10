CREATE DEFINER=`root`@`localhost` PROCEDURE `search_posts`(
    ANONYMOUS_USER_ID int,
    topic_id int,
	author_id int,
	search_text nvarchar(300),
	searchable_forums text,
	`skip` int,
    take int
)
BEGIN  
    set @topic = topic_id;
    set @author = author_id;
    set @search = search_text;
	set @total_count = (
		SELECT count(1) as total_count
		  FROM (
			SELECT p.post_id
			  FROM phpbb_posts p
			 WHERE FIND_IN_SET (p.forum_id, searchable_forums)
			   AND (@topic = 0 OR @topic = p.topic_id)
			   AND (@author = 0 OR @author = p.poster_id)
			   AND (@search IS NULL OR MATCH(p.post_text) AGAINST(@search IN BOOLEAN MODE))
 
			 UNION            

			SELECT p.post_id
			  FROM phpbb_posts p
			 WHERE FIND_IN_SET (p.forum_id, searchable_forums)
			   AND (@topic = 0 OR @topicId = p.topic_id)
			   AND (@author = 0 OR @authorId = p.poster_id)
			   AND (@search IS NULL OR MATCH(p.post_subject) AGAINST(@search IN BOOLEAN MODE))
		) a
    );

    set @sql = CONCAT (
        "WITH ranks AS (
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
	                   case when p.poster_id = ?
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
	                   p.poster_ip as ip,
                       cast(? AS SIGNED) AS total_count
                  FROM phpbb_posts p
                  JOIN phpbb_users a ON p.poster_id = a.user_id
                  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
                  LEFT JOIN ranks r ON a.user_id = r.user_id
                 WHERE FIND_IN_SET (p.forum_id, '", searchable_forums, "')
                   AND (? = 0 OR ? = p.topic_id)
                   AND (? = 0 OR ? = p.poster_id)
                   AND (? IS NULL OR MATCH(p.post_text) AGAINST(? IN BOOLEAN MODE))
  
                 UNION
  
                SELECT p.forum_id,
	                   p.topic_id,
	                   p.post_id,
	                   p.post_subject,
	                   p.post_text,
	                   case when p.poster_id = ?
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
	                   p.poster_ip as ip,
                       cast(? AS SIGNED) AS total_count
                  FROM phpbb_posts p
                  JOIN phpbb_users a ON p.poster_id = a.user_id
                  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
                  LEFT JOIN ranks r ON a.user_id = r.user_id
                 WHERE FIND_IN_SET (p.forum_id, '", searchable_forums, "')
                   AND (? = 0 OR ? = p.topic_id)
                   AND (? = 0 OR ? = p.poster_id)
                   AND (? IS NULL OR MATCH(p.post_subject) AGAINST(? IN BOOLEAN MODE))
   
                 ORDER BY post_time DESC
                 LIMIT ?, 14;");
    
	PREPARE stmt FROM @sql;
	set @skip = `skip`;
    set @anonymous_user_id = ANONYMOUS_USER_ID;
    
	EXECUTE stmt USING @anonymous_iser_id, @total_count, @topic, @topic, @author, @author, @search, @search, @anonymous_iser_id, @total_count, @topic, @topic, @author, @author, @search, @search, @skip;
	DEALLOCATE PREPARE stmt;
    
END