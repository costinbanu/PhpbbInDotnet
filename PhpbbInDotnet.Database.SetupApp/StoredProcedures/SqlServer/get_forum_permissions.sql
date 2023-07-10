/****** Object:  StoredProcedure [dbo].[get_forum_permissions]    Script Date: 03.07.2023 20:35:11 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[get_forum_permissions]  
   @forum_id_param int
AS 
   BEGIN

      SET  XACT_ABORT  ON

      SET  NOCOUNT  ON
      ;
      WITH 
         extended_acl_user AS 
         (
            SELECT au.user_id AS id, u.username AS name, N'User' AS type, au.auth_role_id
            FROM 
               [dbo].phpbb_acl_users  AS au 
                  INNER JOIN [dbo].phpbb_users  AS u 
                  ON au.user_id = u.user_id
            WHERE au.forum_id = @forum_id_param
         ), 
         extended_acl_group AS 
         (
            SELECT ag.group_id AS id, g.group_name AS name, N'Group' AS type, ag.auth_role_id
            FROM 
               [dbo].phpbb_acl_groups  AS ag 
                  INNER JOIN [dbo].phpbb_groups  AS g 
                  ON ag.group_id = g.group_id
            WHERE ag.forum_id = @forum_id_param
         )

      SELECT 
         au.id, 
         au.name, 
         au.type, 
         r.role_id, 
         r.role_name, 
         r.role_description, 
         CASE 
            WHEN r.role_id = au.auth_role_id THEN 1
            ELSE 0
         END AS has_role
      FROM extended_acl_user  AS au, [dbo].phpbb_acl_roles  AS r
      WHERE r.role_type = 'f_'
       UNION ALL
      SELECT 
         ag.id, 
         ag.name, 
         ag.type, 
         r.role_id, 
         r.role_name, 
         r.role_description, 
         CASE 
            WHEN r.role_id = ag.auth_role_id THEN 1
            ELSE 0
         END AS has_role
      FROM extended_acl_group  AS ag, [dbo].phpbb_acl_roles  AS r
      WHERE r.role_type = 'f_'

   END
GO

