CREATE DEFINER=`root`@`localhost` PROCEDURE `sync_user_posts`()
BEGIN
	WITH post_counts AS (
		SELECT poster_id, count(post_id) as post_count
		  FROM phpbb_posts
		 GROUP BY poster_id
	)
	UPDATE phpbb_users u
	   JOIN post_counts c ON u.user_id = c.poster_id
	   SET u.user_posts = c.post_count;
END