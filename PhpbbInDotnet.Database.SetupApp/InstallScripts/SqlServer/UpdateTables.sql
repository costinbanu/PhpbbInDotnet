-- TODO add other statements from MySql ???

ALTER TABLE dbo.phpbb_attachments ADD
	draft_id int NULL
GO

CREATE NONCLUSTERED INDEX draft_id ON dbo.phpbb_attachments
	(
	attach_id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO