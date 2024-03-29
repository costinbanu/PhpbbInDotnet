﻿/****** Object:  StoredProcedure [dbo].[save_new_private_message]    Script Date: 03.07.2023 20:42:36 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[save_new_private_message]
(
	@sender_id int,
	@receiver_id int,
	@subject nvarchar(256),
	@text nvarchar(max),
	@time bigint
)
AS
BEGIN
	SET NOCOUNT ON;

	BEGIN TRANSACTION;

    INSERT INTO phpbb_privmsgs (author_id, to_address, bcc_address, message_subject, message_text, message_time) 
	VALUES (@sender_id, concat('u_', @receiver_id), '', @subject, @text, @time); 
    

	DECLARE @inserted_id int;
	SET @inserted_id = SCOPE_IDENTITY();

    INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) 
	VALUES (@sender_id, @inserted_id, @receiver_id, 0, 1); 
    
	INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) 
	VALUES (@sender_id, @inserted_id, @sender_id, -1, 0);
    
	COMMIT;
END
GO

