CREATE DEFINER=`root`@`localhost` PROCEDURE `get_forum_tree`(exclusionList mediumtext)
BEGIN
WITH RECURSIVE forum_paths AS (
	  SELECT forum_id, 
			 forum_name,
             forum_desc,
             forum_desc_uid,
			 forum_password,
			 cast(forum_id AS CHAR(200)) as path
		FROM phpbb_forums
		WHERE parent_id = 0 /*AND NOT find_in_set(cast(forum_id AS CHAR(200)), exclusionList)*/
	  UNION ALL
	  SELECT f.forum_id, 
			 f.forum_name,
             f.forum_desc,
             f.forum_desc_uid,
			 f.forum_password,
             concat(fp.path, ',', f.forum_id)
		FROM forum_paths AS fp 
		JOIN phpbb_forums AS f
		  ON fp.forum_id = f.parent_id
		/*WHERE NOT find_in_set(cast(f.forum_id AS CHAR(200)), exclusionList)*/
	),
	forums AS (
		SELECT coalesce(parent.forum_id, 0) as forum_id, 
			   parent.forum_name,
               parent.forum_desc,
               parent.forum_desc_uid,
			   parent.forum_password,
			   parent.path as path_to_forum,
			   group_concat(child.forum_id) as children
		  FROM forum_paths parent
		  RIGHT JOIN phpbb_forums child
			 ON parent.forum_id = child.parent_id
		  /*WHERE NOT find_in_set(cast(child.forum_id AS CHAR(200)), exclusionList)*/
		GROUP BY parent.forum_id

		UNION 

		SELECT coalesce(parent.forum_id, 0) as forum_id, 
			   parent.forum_name,
               parent.forum_desc,
               parent.forum_desc_uid,
			   parent.forum_password,
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
		   f.forum_name,
           f.forum_desc,
           f.forum_desc_uid,
		   f.forum_password,
		   f.path_to_forum,
		   f.children,
		   group_concat(t.topic_id) as topics
	  FROM forums f
	  LEFT JOIN phpbb_topics t
		ON f.forum_id = t.forum_id
	 GROUP BY f.forum_id;
END