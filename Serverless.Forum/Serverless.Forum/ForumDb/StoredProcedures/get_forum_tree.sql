CREATE DEFINER=`root`@`localhost` PROCEDURE `get_forum_tree`(exclusion_list_param mediumtext)
BEGIN
    SET @exclusion_list = COALESCE(exclusion_list_param, '');
    
   WITH RECURSIVE bottom_up AS (
	SELECT f.forum_id, 
		   f.forum_type,
           f.forum_name,
		   f.parent_id,
		   f.forum_last_post_id, 
		   f.forum_last_poster_id, 
		   f.forum_last_post_subject, 
		   f.forum_last_post_time, 
		   f.forum_last_poster_name, 
		   f.forum_last_poster_colour
	  FROM phpbb_forums f
     WHERE NOT EXISTS (
		SELECT 1
          FROM phpbb_forums ff
         WHERE ff.parent_id = f.forum_id
    )
      
	 UNION ALL
     
     SELECT parent.forum_id,
            parent.forum_type,
            parent.forum_name,
            parent.parent_id,
			IF(parent.forum_last_post_time > child.forum_last_post_time, parent.forum_last_post_id, child.forum_last_post_id) AS forum_last_post_id,
            IF(parent.forum_last_post_time > child.forum_last_post_time, parent.forum_last_poster_id, child.forum_last_poster_id) AS forum_last_poster_id, 
            IF(parent.forum_last_post_time > child.forum_last_post_time, parent.forum_last_post_subject, child.forum_last_post_subject) AS forum_last_post_subject,
            IF(parent.forum_last_post_time > child.forum_last_post_time, parent.forum_last_post_time, child.forum_last_post_time) AS forum_last_post_time, 
            IF(parent.forum_last_post_time > child.forum_last_post_time, parent.forum_last_poster_name, child.forum_last_poster_name) AS forum_last_poster_name, 
            IF(parent.forum_last_post_time > child.forum_last_post_time, parent.forum_last_poster_colour, child.forum_last_poster_colour) AS forum_last_poster_colour
	   FROM bottom_up child
       JOIN phpbb_forums parent ON parent.forum_id = child.parent_id
	),
	top_down AS (
		SELECT f.forum_id, 
			   cast(f.forum_id AS CHAR(200)) as path,
			   find_in_set(cast(f.forum_id AS CHAR(200)), @exclusion_list) OR COALESCE(f.forum_password, '') <> '' AS is_restricted,
			   1 AS level,
			   COALESCE(f.forum_password, '') <> '' AS has_password,
               f.left_id
		  FROM phpbb_forums f
		 WHERE parent_id = 0 
		  
		UNION ALL
		  
		SELECT child.forum_id, 
			   concat(parent.path, ',', child.forum_id),
			   parent.is_restricted OR find_in_set(cast(child.forum_id AS CHAR(200)), @exclusion_list) OR COALESCE(child.forum_password, '') <> '' AS is_restricted,
			   parent.level + 1 AS level,
			   parent.has_password OR COALESCE(child.forum_password, '') <> '' AS has_password,
               child.left_id
		  FROM top_down AS parent 
		  JOIN phpbb_forums AS child ON parent.forum_id = child.parent_id
	),
	child_forums AS (
		SELECT parent.forum_id,
			   group_concat(child.forum_id) AS child_list
		  FROM phpbb_forums parent
		  LEFT JOIN phpbb_forums child ON parent.forum_id = child.parent_id
		 GROUP BY parent.forum_id
	),
	topics AS (
		SELECT f.forum_id,
			   group_concat(t.topic_id) AS topic_list
		  FROM phpbb_forums f
		  LEFT JOIN phpbb_topics t ON f.forum_id = t.forum_id
		  GROUP BY f.forum_id
	)
	SELECT f.forum_id, 
		   f.forum_type,
		   f.forum_name,
		   f.parent_id,
		   ff.path AS path_to_forum,
		   ff.is_restricted,
		   ff.level,
		   ff.has_password,
           ff.left_id,
		   cf.child_list,
		   t.topic_list,
		   f.forum_last_post_id, 
		   f.forum_last_poster_id, 
		   f.forum_last_post_subject,
		   MAX(f.forum_last_post_time) AS forum_last_post_time, 
		   f.forum_last_poster_name, 
		   f.forum_last_poster_colour
	  FROM bottom_up f
	  JOIN top_down ff ON f.forum_id = ff.forum_id
	  JOIN child_forums cf ON ff.forum_id = cf.forum_id
	  JOIN topics t ON ff.forum_id = t.forum_id
	 GROUP BY f.forum_id

	 UNION ALL
	 
	 SELECT 0 AS forum_id, 
			null, 
			null, 
			null, 
			null, 
			0, 
			0, 
			false, 
            0,
			group_concat(cf.forum_id) as child_list,
			null, 
			null, 
			null, 
			null, 
			null, 
			null, 
			null
	   FROM phpbb_forums cf
	  WHERE cf.parent_id = 0
  
	ORDER BY forum_id;
END