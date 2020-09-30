CREATE DEFINER=`root`@`localhost` PROCEDURE `get_user_details`(user_id_parm int)
BEGIN
	SET @user_id = coalesce(user_id_parm, 1);
    
		WITH user_permissions AS (
		SELECT forum_id, auth_role_id
		FROM phpbb_acl_users
		WHERE user_id = @user_id
	),
	group_permissions AS (
		SELECT gp.forum_id, gp.auth_role_id
		FROM phpbb_acl_groups gp
		JOIN phpbb_users u ON gp.group_id = u.group_id
        WHERE u.user_id = @user_id
	)
	SELECT DISTINCT forum_id, auth_role_id
	FROM user_permissions

	UNION

	SELECT DISTINCT gp.forum_id, gp.auth_role_id
	FROM group_permissions gp
	LEFT JOIN user_permissions up ON gp.forum_id = up.forum_id AND gp.auth_role_id = 16 AND up.auth_role_id in (14, 15, 17)
	WHERE up.forum_id IS NULL;

	SELECT topic_id, post_no
	FROM phpbb_user_topic_post_number
	WHERE user_id = @user_id
	GROUP BY topic_id;
END