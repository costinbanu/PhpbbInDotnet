/****** Object:  StoredProcedure [dbo].[mark_topic_read]    Script Date: 03.07.2023 20:42:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[mark_topic_read]  
   @forumId int,
   @topicId int,
   @userId int,
   @markTime int
AS 
   BEGIN

      SET  XACT_ABORT  ON

      SET  NOCOUNT  ON

      DECLARE
         @existing int = NULL

      SELECT TOP (1) @existing = phpbb_topics_track.mark_time
      FROM [dbo].phpbb_topics_track
      WHERE phpbb_topics_track.user_id = @userId AND phpbb_topics_track.topic_id = @topicId

      IF @existing IS NULL
         INSERT [dbo].phpbb_topics_track(forum_id, mark_time, topic_id, user_id)
            VALUES (@forumId, @markTime, @topicId, @userId)
      ELSE 
         BEGIN
            IF @markTime > @existing
               UPDATE [dbo].phpbb_topics_track
                  SET 
                     forum_id = @forumId, 
                     mark_time = @markTime
               WHERE phpbb_topics_track.user_id = @userId AND phpbb_topics_track.topic_id = @topicId
         END

   END
GO

