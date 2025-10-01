CREATE PROCEDURE [dbo].[get_post_position_in_topic](@topic_id int, @post_id int)
AS
BEGIN
    SET NOCOUNT ON;

	WITH row_numbers AS (
		SELECT post_id, ROW_NUMBER() OVER (ORDER BY post_time) AS rn
		  FROM phpbb_posts
		 WHERE topic_id = @topic_id
	)
	SELECT rn
	FROM row_numbers
	WHERE post_id = @post_id;
END
;

