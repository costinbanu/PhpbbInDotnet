CREATE PROCEDURE [dbo].[sync_orphan_files] (@now bigint, @retention_seconds bigint)
AS
BEGIN
    SET NOCOUNT ON;

	UPDATE a
	   SET a.is_orphan = 1
	  FROM phpbb_attachments a
      LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
	  LEFT JOIN phpbb_drafts d on a.draft_id = d.draft_id
     WHERE p.post_id IS NULL AND d.draft_id IS NULL AND @now - a.filetime > @retention_seconds AND a.is_orphan = 0

END
;

