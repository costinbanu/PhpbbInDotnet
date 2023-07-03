/****** Object:  StoredProcedure [dbo].[get_post_tracking]    Script Date: 03.07.2023 20:39:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[get_post_tracking]  
   @user_id_parm int
AS 
   BEGIN
      /*
      *   *
      *   		https://www.phpbb.com/community/viewtopic.php?t=2165146
      *   		https://www.phpbb.com/community/viewtopic.php?p=2987015
      *   	*
      */

      SET  XACT_ABORT  ON

      SET  NOCOUNT  ON

		DECLARE @user_id INT;
		DECLARE @user_lastmark INT;
		SET @user_id = COALESCE(@user_id_parm, 1);
		SELECT TOP 1 @user_lastmark = user_lastmark
			FROM phpbb_users
			WHERE user_id = @user_id;
	
		WITH marktimes AS (
			SELECT t.topic_id, 
					t.forum_id,
					t.topic_last_post_time,
					tt.mark_time as topic_mark_time, 
					ft.mark_time as forum_mark_time
				FROM phpbb_topics t
				LEFT JOIN phpbb_topics_track tt
						ON tt.topic_id = t.topic_id 
					AND tt.user_id = @user_id
				LEFT JOIN phpbb_forums_track ft
						ON ft.forum_id=t.forum_id 
					AND ft.user_id = @user_id
				WHERE @user_id <> 1
				AND t.topic_last_post_time > coalesce(tt.mark_time, ft.mark_time, @user_lastmark, 0)
		)
		SELECT m.forum_id, 
				m.topic_id, 
				m.topic_last_post_time, 
				m.topic_mark_time, 
				m.forum_mark_time, 
				0 as user_lastmark, 
				string_agg(cast(p.post_id as nvarchar(max)), ',') AS post_ids
			FROM marktimes m
			JOIN phpbb_posts p
			ON m.topic_id = p.topic_id
			WHERE p.poster_id <> @user_id
			AND @user_id <> 1
			AND post_time > @user_lastmark
			AND p.post_time > coalesce(topic_mark_time, forum_mark_time, @user_lastmark, 0)
			GROUP BY m.forum_id, m.topic_id, m.topic_last_post_time, m.topic_mark_time, m.forum_mark_time;
		END
GO

