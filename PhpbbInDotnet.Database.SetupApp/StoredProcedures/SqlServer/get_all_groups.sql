/****** Object:  StoredProcedure [dbo].[get_all_groups]    Script Date: 03.07.2023 20:33:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[get_all_groups]

AS
BEGIN
    SET NOCOUNT ON;

    SELECT g.group_id AS id, 
		   g.group_name AS [name],
		   g.group_desc AS [desc],
	  	   g.group_rank AS [rank],
	  	   concat('#', g.group_colour) AS color,
	  	   g.group_edit_time AS edit_time,
	  	   g.group_user_upload_size AS upload_limit,
	  	   coalesce(
			   (SELECT TOP 1 r.auth_role_id 
			   FROM phpbb_acl_groups r  
			   WHERE g.group_id = r.group_id AND r.forum_id = 0)
			   , 0
		   ) as [role]
	  FROM phpbb_groups g;
END;
GO

