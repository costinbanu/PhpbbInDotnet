CREATE DEFINER=`root`@`localhost` PROCEDURE `get_posts`(topic_id_parm int, anonymous_user_id int, `order` nvarchar(20), `skip` int, take int, include_reports bool)
BEGIN
    SET @topic_id = topic_id_parm;
	SET @total_count = (SELECT COUNT(1) FROM phpbb_posts WHERE topic_id = @topic_id);
	
    START TRANSACTION;
    
	SET @sql = concat(
		"CREATE TEMPORARY TABLE tmp_posts
         WITH ranks AS (
				SELECT DISTINCT u.user_id, 
					   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
					   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
				  FROM phpbb_users u
				  JOIN phpbb_groups g ON u.group_id = g.group_id
				  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
				  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
			)
			SELECT 
				   p.forum_id,
				   p.topic_id,
				   p.post_id,
				   p.post_subject,
				   p.post_text,
				   CASE WHEN p.poster_id = ?
						THEN p.post_username 
						ELSE a.username
				   END AS author_name,
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
			  LEFT JOIN phpbb_users a ON p.poster_id = a.user_id
			  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
			  LEFT JOIN ranks r ON a.user_id = r.user_id
			  WHERE topic_id = ? 
			  ORDER BY 
				CASE             
					WHEN ? = 'ASC' THEN post_time 
				END ASC, 
				CASE 
					WHEN ? = 'DESC' THEN post_time 
				END DESC 
			  LIMIT ?, ?;");
              
	SET @anonymous_user_id = anonymous_user_id;
    SET @order = `order`;
    SET @skip = `skip`;
    SET @take = take;
	PREPARE stmt FROM @sql;
	EXECUTE stmt USING @anonymous_user_id, @total_count, @topic_id, @order, @order, @skip, @take;
	DEALLOCATE PREPARE stmt;
    
    SELECT *
      FROM tmp_posts;
      
	SELECT a.*
      FROM phpbb_attachments a
      JOIN tmp_posts p ON a.post_msg_id = p.post_id;
      
	IF include_reports = 1
    THEN
		SELECT r.report_id AS id, 
			   rr.reason_title, 
               rr.reason_description, 
               r.report_text AS details, 
               r.user_id AS reporter_id, 
               u.username AS reporter_username, 
               r.post_id,
               r.report_time,
               r.report_closed
		  FROM phpbb_reports r
		  JOIN phpbb_reports_reasons rr ON r.reason_id = rr.reason_id
		  JOIN phpbb_users u ON r.user_id = u.user_id
		  JOIN tmp_posts p ON r.post_id = p.post_id;
	END IF;
    
    DROP TABLE tmp_posts;
    
    COMMIT;
END