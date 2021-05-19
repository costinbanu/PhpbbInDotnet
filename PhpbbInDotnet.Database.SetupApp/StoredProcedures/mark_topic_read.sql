CREATE DEFINER=`root`@`localhost` PROCEDURE `mark_topic_read`(forumId int, topicId int, userId int, markTime int)
BEGIN
	SELECT mark_time 
      INTO @existing
      FROM phpbb_topics_track
     WHERE user_id = userId 
       AND topic_id = topicId
     LIMIT 1;
    
    IF @existing IS NULL
	THEN
		INSERT INTO phpbb_topics_track (forum_id, mark_time, topic_id, user_id) 
        VALUES (forumId, markTime, topicId, userId);
	ELSE IF markTime > @existing
		THEN
			UPDATE phpbb_topics_track 
			   SET forum_id = forumId, mark_time = markTime 
			 WHERE user_id = userId 
               AND topic_id = topicId;
		END IF;
	END IF;
END