-- TODO add other statements from MySql ???

CREATE FULLTEXT CATALOG ft_catalog WITH ACCENT_SENSITIVITY = OFF;
CREATE FULLTEXT INDEX ON [dbo].[phpbb_posts](
[post_subject] LANGUAGE 'English', 
[post_text] LANGUAGE 'English')
KEY INDEX [PK_phpbb_posts_post_id]ON ([ft_catalog], FILEGROUP [PRIMARY])
WITH (CHANGE_TRACKING = AUTO, STOPLIST = SYSTEM);
CREATE FULLTEXT INDEX ON [dbo].[phpbb_attachments](
[attach_comment] LANGUAGE 'English', 
[real_filename] LANGUAGE 'English')
KEY INDEX [PK_phpbb_attachments_attach_id]ON ([ft_catalog], FILEGROUP [PRIMARY])
WITH (CHANGE_TRACKING = AUTO, STOPLIST = SYSTEM);

ALTER TABLE dbo.phpbb_attachments ADD
	draft_id int NULL
;

CREATE NONCLUSTERED INDEX draft_id ON dbo.phpbb_attachments
	(
	attach_id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
;

ALTER TABLE dbo.phpbb_attachments ADD
	order_in_post int NULL
;