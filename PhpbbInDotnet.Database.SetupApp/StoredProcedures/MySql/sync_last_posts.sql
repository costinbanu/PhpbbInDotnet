﻿CREATE PROCEDURE `sync_last_posts`(ANONYMOUS_USER_ID int, ANONYMOUS_USER_NAME nvarchar(255), DEFAULT_USER_COLOR nvarchar(6))
BEGIN
	CALL sync_forum_with_posts(null, ANONYMOUS_USER_ID, ANONYMOUS_USER_NAME, DEFAULT_USER_COLOR);
    CALL sync_topic_with_posts(null, ANONYMOUS_USER_ID, ANONYMOUS_USER_NAME, DEFAULT_USER_COLOR);
END