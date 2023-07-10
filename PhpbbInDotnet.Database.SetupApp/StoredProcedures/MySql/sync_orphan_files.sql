CREATE DEFINER=`root`@`localhost` PROCEDURE `sync_orphan_files`(now int(11), retention_seconds int(11))
BEGIN
	UPDATE phpbb_attachments a
	  LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
	   SET a.is_orphan = 1
     WHERE p.post_id IS NULL AND @now - a.filetime > @retention_seconds AND a.is_orphan = 0;
END