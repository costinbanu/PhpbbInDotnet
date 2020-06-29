CREATE DEFINER=`root`@`localhost` PROCEDURE `get_forum_tree`(exclusion_list_param mediumtext, forum_id_param int, full_traversal_param boolean)
BEGIN
	SET @forum_id = COALESCE(forum_id_param, 0);
    SET @exclusion_list = COALESCE(exclusion_list_param, '');
	SET @full_traversal = COALESCE(full_traversal_param, false);
    
		WITH RECURSIVE forum_paths AS (
	  SELECT f.forum_id, 
			 cast(f.forum_id AS CHAR(200)) as path,
             1 AS level,
             f.parent_id
		FROM phpbb_forums f
		WHERE parent_id = 0 AND NOT find_in_set(cast(forum_id AS CHAR(200)), @exclusion_list)
	  
      UNION ALL
	  
      SELECT f.forum_id, 
             concat(fp.path, ',', f.forum_id),
             fp.level + 1 as level,
             f.parent_id
		FROM forum_paths AS fp 
		JOIN phpbb_forums AS f
		  ON fp.forum_id = f.parent_id
		WHERE NOT find_in_set(cast(f.forum_id AS CHAR(200)), @exclusion_list)
	),
    
	forums AS (
		SELECT coalesce(parent.forum_id, 0) as forum_id, 
               coalesce(parent.level, 0) as level,
               parent.parent_id,
			   parent.path as path_to_forum,
			   group_concat(child.forum_id) as children
		  FROM forum_paths parent
		  RIGHT JOIN phpbb_forums child
			 ON parent.forum_id = child.parent_id
		  WHERE NOT find_in_set(cast(child.forum_id AS CHAR(200)), @exclusion_list)
		GROUP BY parent.forum_id

		UNION 

		SELECT coalesce(parent.forum_id, 0) as forum_id, 
               coalesce(parent.level, 0) as level,
               parent.parent_id,
			   coalesce(parent.path, '0') as path_to_forum,
			   group_concat(no_child.forum_id) as children
		  FROM forum_paths parent
		  LEFT JOIN phpbb_forums no_child
			ON parent.forum_id = no_child.parent_id
		 WHERE no_child.forum_id IS NULL
		GROUP BY parent.forum_id

		ORDER BY forum_id
	)
	SELECT f.forum_id,
		   f.level,
           f.parent_id,
		   f.path_to_forum,
		   f.children AS forum_children,
		   group_concat(t.topic_id) as topics
	  FROM forums f
	  LEFT JOIN phpbb_topics t
		ON f.forum_id = t.forum_id AND f.level = 0
	 WHERE @forum_id = 0 OR f.forum_id = @forum_id OR f.parent_id = @forum_id
	 GROUP BY f.forum_id
     HAVING @full_traversal OR f.level <= MAX(f.level) + 2
     ORDER BY level;
END