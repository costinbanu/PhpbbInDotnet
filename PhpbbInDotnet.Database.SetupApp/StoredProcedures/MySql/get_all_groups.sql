CREATE PROCEDURE `get_all_groups` ()
BEGIN
	SELECT g.group_id AS id, 
		   g.group_name AS `name`,
		   g.group_desc AS `desc`,
	  	   g.group_rank AS `rank`,
	  	   concat('#', g.group_colour) AS color,
	  	   g.group_edit_time AS edit_time,
	  	   g.group_user_upload_size AS upload_limit,
	  	   coalesce(
			   (SELECT r.auth_role_id 
			   FROM phpbb_acl_groups r  
			   WHERE g.group_id = r.group_id AND r.forum_id = 0
               LIMIT 1)
			   , 0
		   ) as `role`
	  FROM phpbb_groups g;
END
