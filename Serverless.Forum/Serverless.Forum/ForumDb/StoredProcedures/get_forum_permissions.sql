CREATE DEFINER=`root`@`localhost` PROCEDURE `get_forum_permissions`(forum_id_param int)
BEGIN
	WITH extended_acl_user AS (
		SELECT
			au.user_id as id
		  , u.username as "name"
		  ,'User'      as "type"
		  , au.auth_role_id
		FROM phpbb_acl_users au
		JOIN phpbb_users u ON au.user_id = u.user_id
		WHERE au.forum_id = forum_id_param
	)
	, extended_acl_group AS (
		SELECT
			ag.group_id  as id
		  , g.group_name as "name"
		  ,'Group'       as "type"
		  , ag.auth_role_id
		FROM phpbb_acl_groups ag
		JOIN phpbb_groups g ON ag.group_id = g.group_id
		WHERE ag.forum_id = forum_id_param
	)
	SELECT
		au.id
	  , au.name
	  , au.type
      , r.role_id
	  , r.role_name
	  , r.role_description
	  , r.has_role
	FROM extended_acl_user au
	, LATERAL (
		SELECT
			r1.role_id
		  , r1.role_name
		  , r1.role_description
		  , CASE WHEN r1.role_id = au.auth_role_id
			THEN true
			ELSE false
			END AS has_role
		FROM phpbb_acl_roles r1
		WHERE r1.role_type = 'f_'
	) AS r
	
    UNION ALL    
    
	SELECT
		ag.id
	  , ag.name
	  , ag.type
      , r.role_id
	  , r.role_name
	  , r.role_description
	  , r.has_role
	FROM extended_acl_group ag
	, LATERAL (
		SELECT
			r1.role_id
		  , r1.role_name
		  , r1.role_description
		  , CASE WHEN r1.role_id = ag.auth_role_id
			THEN true
			ELSE false
			END AS has_role
		FROM phpbb_acl_roles r1
		WHERE r1.role_type = 'f_'
	) AS r;
END