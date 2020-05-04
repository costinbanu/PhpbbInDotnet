CREATE DEFINER=`root`@`localhost` PROCEDURE `get_user_details`(user_id_parm int)
BEGIN
	with user_groups as (
		select group_id
		from phpbb_user_group
		where user_id = user_id_parm
	),
	user_permissions as (
		select *
		from phpbb_acl_users
		where user_id = user_id_parm
	),
	group_permissions as (
		select gp.*
		from phpbb_acl_groups gp
		join user_groups ug
		on gp.group_id = ug.group_id
		left outer join user_permissions up
		on gp.forum_id = up.forum_id
		where up.forum_id is null
	)
	select distinct forum_id, auth_option_id, auth_role_id, auth_setting
	from user_permissions
	union
	select forum_id, auth_option_id, auth_role_id, auth_setting
	from group_permissions;
    
    select group_id
	from phpbb_user_group
	where user_id = user_id_parm;
    
    select topic_id, post_no
    from phpbb_user_topic_post_number
    where user_id = user_id_parm
    group by topic_id;
END