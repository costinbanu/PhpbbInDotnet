CREATE DEFINER=`root`@`localhost` PROCEDURE `get_forum_tree`()
BEGIN
  
  WITH child_forums AS (
	SELECT parent.forum_id,
		   group_concat(child.forum_id ORDER BY child.left_id) AS children
	  FROM phpbb_forums parent
	  LEFT JOIN phpbb_forums child ON parent.forum_id = child.parent_id
	 GROUP BY parent.forum_id
)
SELECT f.forum_id, 
		   f.forum_type,
		   f.forum_name,
		   f.parent_id,
           f.left_id,
		   f.forum_desc,
		   f.forum_desc_uid,
           COALESCE(f.forum_password, '') <> '' AS has_password,
		   cf.children,
		   f.forum_last_post_id, 
		   f.forum_last_poster_id, 
		   f.forum_last_post_subject,
		   f.forum_last_post_time, 
		   f.forum_last_poster_name, 
		   f.forum_last_poster_colour
	  FROM phpbb_forums f
	  LEFT JOIN child_forums cf ON f.forum_id = cf.forum_id
      
      UNION ALL (
      
		  SELECT 0, 
				null, 
				null, 
				null, 
				null, 
				null,
				null,
				false,
				group_concat(cf.forum_id ORDER BY cf.left_id),
				null, 
				null, 
				null, 
				null, 
				null, 
				null
		   FROM phpbb_forums cf
		  WHERE cf.parent_id = 0
	  );
      
END