CREATE PROCEDURE `sync_forum_with_posts`(forum_ids text, anonymous_user_id int, anonymous_username nvarchar(255), default_user_colour nvarchar(6))
BEGIN
	IF forum_ids = null OR ltrim(rtrim(forum_ids)) = '' THEN
		SET forum_ids = '';
	END IF;
    
	DROP TEMPORARY TABLE IF EXISTS forum_ids_tmp;
    CREATE TEMPORARY TABLE forum_ids_tmp (INDEX (forum_id)) AS
	SELECT forum_id
      FROM phpbb_forums
	 WHERE FIND_IN_SET(forum_id, forum_ids);
	
	DROP TEMPORARY TABLE IF EXISTS last_posts;
    CREATE TEMPORARY TABLE last_posts (INDEX (post_id)) AS
    WITH maxes AS (
		SELECT forum_id, MAX(post_time) AS post_time
		  FROM phpbb_posts
		 GROUP BY forum_id
    )
	SELECT DISTINCT p.*
	  FROM phpbb_posts p
	  JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time;
	
	UPDATE phpbb_forums f
      JOIN last_posts lp ON f.forum_id = lp.forum_id
      JOIN forum_ids_tmp fit ON f.forum_id = fit.forum_id
      LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id and u.user_id <> anonymous_user_id
	   SET f.forum_last_post_id = lp.post_id,
		   f.forum_last_poster_id = COALESCE(u.user_id, anonymous_user_id),
		   f.forum_last_post_subject = lp.post_subject,
		   f.forum_last_post_time = lp.post_time,
		   f.forum_last_poster_name = COALESCE(u.username, lp.post_username, anonymous_username),
		   f.forum_last_poster_colour = COALESCE(u.user_colour, default_user_colour)  
	 WHERE lp.post_id <> f.forum_last_post_id;
END