CREATE DEFINER=`root`@`localhost` PROCEDURE `save_new_private_message`(
	sender_id int,
	receiver_id int,
	`subject` nvarchar(256),
	`text` text,
	`time` bigint
)
BEGIN
	START TRANSACTION;

    INSERT INTO phpbb_privmsgs (author_id, to_address, bcc_address, message_subject, `message_text`, message_time) 
	VALUES (sender_id, concat('u_', receiver_id), '', `subject`, `text`, `time`); 
    
	SET @inserted_id = LAST_INSERT_ID();

    INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) 
	VALUES (sender_id, @inserted_id, receiver_id, 0, 1); 
    
	INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) 
	VALUES (sender_id, @inserted_id, sender_id, -1, 0);
    
	COMMIT;
END