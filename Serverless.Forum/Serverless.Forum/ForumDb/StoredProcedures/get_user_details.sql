﻿CREATE DEFINER=`root`@`localhost` PROCEDURE `get_user_details`(user_id_parm int)
BEGIN
	SET @user_id = coalesce(user_id_parm, 1);
    
	WITH user_groups AS (
		SELECT group_id
		FROM phpbb_user_group
		WHERE user_id = @user_id
	),
	user_permissions AS (
		SELECT forum_id, auth_role_id
		FROM phpbb_acl_users
		WHERE user_id = @user_id
	),
	group_permissions AS (
		SELECT gp.forum_id, gp.auth_role_id
		FROM phpbb_acl_groups gp
		JOIN user_groups ug ON gp.group_id = ug.group_id
	)
	SELECT DISTINCT forum_id, auth_role_id
	FROM user_permissions

	UNION

	SELECT DISTINCT forum_id, auth_role_id
	FROM group_permissions;

	SELECT group_id
	FROM phpbb_user_group
	WHERE user_id = @user_id;

	SELECT topic_id, post_no
	FROM phpbb_user_topic_post_number
	WHERE user_id = @user_id
	GROUP BY topic_id;
END