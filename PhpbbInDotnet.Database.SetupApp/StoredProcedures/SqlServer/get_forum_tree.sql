/****** Object:  StoredProcedure [dbo].[get_forum_tree]    Script Date: 04.07.2023 18:42:26 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[get_forum_tree]
AS 
   BEGIN

    SET  XACT_ABORT  ON;
    SET  NOCOUNT  ON;

    SELECT parent.forum_id, 
		   string_agg(cast(child.forum_id as nvarchar(max)), ',') WITHIN GROUP (ORDER BY child.left_id) AS children
	  INTO #child_forums
      FROM phpbb_forums parent 
      LEFT JOIN phpbb_forums child ON parent.forum_id = child.parent_id
     GROUP BY parent.forum_id;

	SELECT f.forum_id, 
           f.forum_type, 
           f.forum_name, 
           f.parent_id, 
           f.left_id, 
           f.forum_desc, 
           f.forum_desc_uid, 
           CASE WHEN (coalesce(f.forum_password, N'') <> N'') THEN 1 ELSE 0 END AS has_password, 
           cf.children, 
           f.forum_last_post_id, 
           f.forum_last_poster_id, 
           f.forum_last_post_subject, 
           f.forum_last_post_time, 
           f.forum_last_poster_name, 
           f.forum_last_poster_colour
      FROM phpbb_forums f 
      LEFT JOIN #child_forums cf ON f.forum_id = cf.forum_id
     
	 UNION ALL (

		SELECT 0, 
			   NULL, 
			   NULL, 
			   NULL, 
			   NULL, 
			   NULL, 
			   NULL, 
			   0, 
			   string_agg(cast(cf.forum_id as nvarchar(max)), ',') WITHIN GROUP (ORDER BY cf.left_id),
			   NULL, 
			   NULL, 
			   NULL, 
			   NULL, 
			   NULL, 
			   NULL
		  FROM phpbb_forums cf
		 WHERE cf.parent_id = 0
	);
      
END
