 use forum;
 
 IF NOT EXISTS(SELECT * FROM sys.schemas WHERE [name] = N'dbo')      
     EXEC (N'CREATE SCHEMA [dbo]')                                   
 GO                                                               
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_acl_groups'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_acl_groups'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_acl_groups]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_acl_groups]
(
   [group_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [auth_option_id] int  NOT NULL,
   [auth_role_id] int  NOT NULL,
   [auth_setting] smallint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_acl_groups',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_acl_groups'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_acl_options'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_acl_options'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_acl_options]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_acl_options]
(
   [auth_option_id] int IDENTITY(120, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [auth_option] nvarchar(50)  NOT NULL,
   [is_global] tinyint  NOT NULL,
   [is_local] tinyint  NOT NULL,
   [founder_only] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_acl_options',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_acl_options'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_acl_roles'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_acl_roles'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_acl_roles]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_acl_roles]
(
   [role_id] int IDENTITY(25, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [role_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [role_description] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [role_type] nvarchar(10)  NOT NULL,
   [role_order] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_acl_roles',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_acl_roles'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_acl_roles_data'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_acl_roles_data'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_acl_roles_data]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_acl_roles_data]
(
   [role_id] int  NOT NULL,
   [auth_option_id] int  NOT NULL,
   [auth_setting] smallint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_acl_roles_data',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_acl_roles_data'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_acl_users'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_acl_users'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_acl_users]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_acl_users]
(
   [user_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [auth_option_id] int  NOT NULL,
   [auth_role_id] int  NOT NULL,
   [auth_setting] smallint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_acl_users',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_acl_users'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_attachments]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_attachments]
(
   [attach_id] int IDENTITY(211355, 1)  NOT NULL,
   [post_msg_id] int  NOT NULL,
   [topic_id] int  NOT NULL,
   [in_message] tinyint  NOT NULL,
   [poster_id] int  NOT NULL,
   [is_orphan] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [physical_filename] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [real_filename] nvarchar(255)  NOT NULL,
   [download_count] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [attach_comment] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [extension] nvarchar(100)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [mimetype] nvarchar(100)  NOT NULL,
   [filesize] bigint  NOT NULL,
   [filetime] bigint  NOT NULL,
   [thumbnail] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_attachments',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_attachments'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_banlist'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_banlist'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_banlist]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_banlist]
(
   [ban_id] int IDENTITY(269, 1)  NOT NULL,
   [ban_userid] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [ban_ip] nvarchar(40)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [ban_email] nvarchar(100)  NOT NULL,
   [ban_start] bigint  NOT NULL,
   [ban_end] bigint  NOT NULL,
   [ban_exclude] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [ban_reason] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [ban_give_reason] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_banlist',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_banlist'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_bbcodes'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_bbcodes'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_bbcodes]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_bbcodes]
(
   [bbcode_id] smallint IDENTITY(22, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_tag] nvarchar(16)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_helpline] nvarchar(255)  NOT NULL,
   [display_on_posting] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_match] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_tpl] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [first_pass_match] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [first_pass_replace] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [second_pass_match] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [second_pass_replace] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_bbcodes',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_bbcodes'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_bookmarks'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_bookmarks'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_bookmarks]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_bookmarks]
(
   [topic_id] int  NOT NULL,
   [user_id] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_bookmarks',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_bookmarks'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_bots'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_bots'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_bots]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_bots]
(
   [bot_id] int IDENTITY(52, 1)  NOT NULL,
   [bot_active] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bot_name] nvarchar(255)  NOT NULL,
   [user_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bot_agent] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bot_ip] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_bots',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_bots'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_captcha_answers'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_captcha_answers'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_captcha_answers]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_captcha_answers]
(
   [question_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [answer_text] nvarchar(255)  NOT NULL,
   [id] int IDENTITY(13, 1)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_captcha_answers',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_captcha_answers'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_captcha_questions'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_captcha_questions'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_captcha_questions]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_captcha_questions]
(
   [question_id] int IDENTITY(7, 1)  NOT NULL,
   [strict] tinyint  NOT NULL,
   [lang_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_iso] nvarchar(30)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [question_text] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_captcha_questions',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_captcha_questions'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_config'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_config'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_config]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_config]
(

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [config_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [config_value] nvarchar(255)  NOT NULL,
   [is_dynamic] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_config',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_config'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_confirm'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_confirm'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_confirm]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_confirm]
(

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [confirm_id] nvarchar(32)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_id] nvarchar(32)  NOT NULL,
   [confirm_type] smallint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [code] nvarchar(8)  NOT NULL,
   [seed] bigint  NOT NULL,
   [attempts] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_confirm',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_confirm'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_disallow'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_disallow'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_disallow]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_disallow]
(
   [disallow_id] int IDENTITY(3, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [disallow_username] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_disallow',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_disallow'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_drafts'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_drafts'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_drafts]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_drafts]
(
   [draft_id] int IDENTITY(1557, 1)  NOT NULL,
   [user_id] int  NOT NULL,
   [topic_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [save_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [draft_subject] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [draft_message] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_drafts',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_drafts'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_extension_groups'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_extension_groups'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_extension_groups]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_extension_groups]
(
   [group_id] int IDENTITY(10, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_name] nvarchar(255)  NOT NULL,
   [cat_id] smallint  NOT NULL,
   [allow_group] tinyint  NOT NULL,
   [download_mode] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [upload_icon] nvarchar(255)  NOT NULL,
   [max_filesize] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [allowed_forums] nvarchar(max)  NOT NULL,
   [allow_in_pm] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_extension_groups',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_extension_groups'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_extensions'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_extensions'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_extensions]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_extensions]
(
   [extension_id] int IDENTITY(67, 1)  NOT NULL,
   [group_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [extension] nvarchar(100)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_extensions',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_extensions'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_forums'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_forums'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_forums]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_forums]
(
   [forum_id] int IDENTITY(573, 1)  NOT NULL,
   [parent_id] int  NOT NULL,
   [left_id] int  NOT NULL,
   [right_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_parents] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_desc] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_desc_bitfield] nvarchar(255)  NOT NULL,
   [forum_desc_options] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_desc_uid] nvarchar(8)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_link] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_password] nvarchar(40)  NOT NULL,
   [forum_style] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_image] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_rules] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_rules_link] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_rules_bitfield] nvarchar(255)  NOT NULL,
   [forum_rules_options] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_rules_uid] nvarchar(8)  NOT NULL,
   [forum_topics_per_page] smallint  NOT NULL,
   [forum_type] smallint  NOT NULL,
   [forum_status] smallint  NOT NULL,
   [forum_posts] int  NOT NULL,
   [forum_topics] int  NOT NULL,
   [forum_topics_real] int  NOT NULL,
   [forum_last_post_id] int  NOT NULL,
   [forum_last_poster_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_last_post_subject] nvarchar(255)  NOT NULL,
   [forum_last_post_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_last_poster_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [forum_last_poster_colour] nvarchar(6)  NOT NULL,
   [forum_flags] smallint  NOT NULL,
   [forum_options] bigint  NOT NULL,
   [display_subforum_list] tinyint  NOT NULL,
   [display_on_index] tinyint  NOT NULL,
   [enable_indexing] tinyint  NOT NULL,
   [enable_icons] tinyint  NOT NULL,
   [enable_prune] tinyint  NOT NULL,
   [prune_next] bigint  NOT NULL,
   [prune_days] int  NOT NULL,
   [prune_viewed] int  NOT NULL,
   [prune_freq] int  NOT NULL,
   [forum_edit_time] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_forums',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_forums'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_forums_access'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_forums_access'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_forums_access]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_forums_access]
(
   [forum_id] int  NOT NULL,
   [user_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_id] nchar(32)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_forums_access',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_forums_access'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_forums_track'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_forums_track'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_forums_track]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_forums_track]
(
   [user_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [mark_time] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_forums_track',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_forums_track'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_forums_watch'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_forums_watch'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_forums_watch]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_forums_watch]
(
   [forum_id] int  NOT NULL,
   [user_id] int  NOT NULL,
   [notify_status] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_forums_watch',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_forums_watch'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_groups'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_groups'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_groups]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_groups]
(
   [group_id] int IDENTITY(18, 1)  NOT NULL,
   [group_type] smallint  NOT NULL,
   [group_founder_manage] tinyint  NOT NULL,
   [group_skip_auth] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_desc] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_desc_bitfield] nvarchar(255)  NOT NULL,
   [group_desc_options] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_desc_uid] nvarchar(8)  NOT NULL,
   [group_display] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_avatar] nvarchar(255)  NOT NULL,
   [group_avatar_type] smallint  NOT NULL,
   [group_avatar_width] int  NOT NULL,
   [group_avatar_height] int  NOT NULL,
   [group_rank] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_colour] nvarchar(6)  NOT NULL,
   [group_sig_chars] int  NOT NULL,
   [group_receive_pm] tinyint  NOT NULL,
   [group_message_limit] int  NOT NULL,
   [group_max_recipients] int  NOT NULL,
   [group_legend] tinyint  NOT NULL,
   [group_user_upload_size] bigint  NOT NULL,
   [group_edit_time] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_groups',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_groups'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_icons'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_icons'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_icons]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_icons]
(
   [icons_id] int IDENTITY(13, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [icons_url] nvarchar(255)  NOT NULL,
   [icons_width] smallint  NOT NULL,
   [icons_height] smallint  NOT NULL,
   [icons_order] int  NOT NULL,
   [display_on_posting] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_icons',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_icons'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_lang'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_lang'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_lang]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_lang]
(
   [lang_id] smallint IDENTITY(10, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_iso] nvarchar(30)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_dir] nvarchar(30)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_english_name] nvarchar(100)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_local_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_author] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_lang',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_lang'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_log]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_log]
(
   [log_id] int IDENTITY(117716, 1)  NOT NULL,
   [log_type] smallint  NOT NULL,
   [user_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [topic_id] int  NOT NULL,
   [reportee_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [log_ip] nvarchar(40)  NOT NULL,
   [log_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [log_operation] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [log_data] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_log',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_log'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_moderator_cache'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_moderator_cache'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_moderator_cache]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_moderator_cache]
(
   [forum_id] int  NOT NULL,
   [user_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [username] nvarchar(255)  NOT NULL,
   [group_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [group_name] nvarchar(255)  NOT NULL,
   [display_on_index] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_moderator_cache',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_moderator_cache'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_modules'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_modules'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_modules]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_modules]
(
   [module_id] int IDENTITY(199, 1)  NOT NULL,
   [module_enabled] tinyint  NOT NULL,
   [module_display] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [module_basename] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [module_class] nvarchar(10)  NOT NULL,
   [parent_id] int  NOT NULL,
   [left_id] int  NOT NULL,
   [right_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [module_langname] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [module_mode] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [module_auth] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_modules',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_modules'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_poll_options'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_poll_options'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_poll_options]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_poll_options]
(
   [poll_option_id] smallint  NOT NULL,
   [topic_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [poll_option_text] nvarchar(max)  NOT NULL,
   [poll_option_total] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_poll_options',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_poll_options'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_poll_votes'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_poll_votes'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_poll_votes]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_poll_votes]
(
   [topic_id] int  NOT NULL,
   [poll_option_id] smallint  NOT NULL,
   [vote_user_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [vote_user_ip] nvarchar(40)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_poll_votes',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_poll_votes'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_posts]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_posts]
(
   [post_id] int IDENTITY(455528, 1)  NOT NULL,
   [topic_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [poster_id] int  NOT NULL,
   [icon_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [poster_ip] nvarchar(40)  NOT NULL,
   [post_time] bigint  NOT NULL,
   [post_approved] tinyint  NOT NULL,
   [post_reported] tinyint  NOT NULL,
   [enable_bbcode] tinyint  NOT NULL,
   [enable_smilies] tinyint  NOT NULL,
   [enable_magic_url] tinyint  NOT NULL,
   [enable_sig] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [post_username] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_unicode_ci.
   */

   [post_subject] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_unicode_ci.
   */

   [post_text] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [post_checksum] nvarchar(32)  NOT NULL,
   [post_attachment] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_bitfield] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_uid] nvarchar(8)  NOT NULL,
   [post_postcount] tinyint  NOT NULL,
   [post_edit_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [post_edit_reason] nvarchar(255)  NOT NULL,
   [post_edit_user] int  NOT NULL,
   [post_edit_count] int  NOT NULL,
   [post_edit_locked] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_posts',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_posts'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_privmsgs'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_privmsgs'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_privmsgs]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_privmsgs]
(
   [msg_id] int IDENTITY(30806, 1)  NOT NULL,
   [root_level] int  NOT NULL,
   [author_id] int  NOT NULL,
   [icon_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [author_ip] nvarchar(40)  NOT NULL,
   [message_time] bigint  NOT NULL,
   [enable_bbcode] tinyint  NOT NULL,
   [enable_smilies] tinyint  NOT NULL,
   [enable_magic_url] tinyint  NOT NULL,
   [enable_sig] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [message_subject] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [message_text] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [message_edit_reason] nvarchar(255)  NOT NULL,
   [message_edit_user] int  NOT NULL,
   [message_attachment] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_bitfield] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_uid] nvarchar(8)  NOT NULL,
   [message_edit_time] bigint  NOT NULL,
   [message_edit_count] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [to_address] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bcc_address] nvarchar(max)  NOT NULL,
   [message_reported] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_privmsgs',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_privmsgs'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_privmsgs_folder'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_privmsgs_folder'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_privmsgs_folder]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_privmsgs_folder]
(
   [folder_id] int IDENTITY(6, 1)  NOT NULL,
   [user_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [folder_name] nvarchar(255)  NOT NULL,
   [pm_count] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_privmsgs_folder',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_privmsgs_folder'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_privmsgs_rules'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_privmsgs_rules'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_privmsgs_rules]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_privmsgs_rules]
(
   [rule_id] int IDENTITY(7, 1)  NOT NULL,
   [user_id] int  NOT NULL,
   [rule_check] int  NOT NULL,
   [rule_connection] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [rule_string] nvarchar(255)  NOT NULL,
   [rule_user_id] int  NOT NULL,
   [rule_group_id] int  NOT NULL,
   [rule_action] int  NOT NULL,
   [rule_folder_id] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_privmsgs_rules',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_privmsgs_rules'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_privmsgs_to'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_privmsgs_to'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_privmsgs_to]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_privmsgs_to]
(
   [msg_id] int  NOT NULL,
   [user_id] int  NOT NULL,
   [author_id] int  NOT NULL,
   [pm_deleted] tinyint  NOT NULL,
   [pm_new] tinyint  NOT NULL,
   [pm_unread] tinyint  NOT NULL,
   [pm_replied] tinyint  NOT NULL,
   [pm_marked] tinyint  NOT NULL,
   [pm_forwarded] tinyint  NOT NULL,
   [folder_id] int  NOT NULL,
   [id] bigint IDENTITY(29752, 1)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_privmsgs_to',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_privmsgs_to'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_profile_fields'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_profile_fields'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_profile_fields]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_profile_fields]
(
   [field_id] int IDENTITY(1, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_name] nvarchar(255)  NOT NULL,
   [field_type] smallint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_ident] nvarchar(20)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_length] nvarchar(20)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_minlen] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_maxlen] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_novalue] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_default_value] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [field_validation] nvarchar(20)  NOT NULL,
   [field_required] tinyint  NOT NULL,
   [field_show_on_reg] tinyint  NOT NULL,
   [field_show_on_vt] tinyint  NOT NULL,
   [field_show_profile] tinyint  NOT NULL,
   [field_hide] tinyint  NOT NULL,
   [field_no_view] tinyint  NOT NULL,
   [field_active] tinyint  NOT NULL,
   [field_order] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_profile_fields',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_profile_fields'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_profile_fields_data'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_profile_fields_data'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_profile_fields_data]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_profile_fields_data]
(
   [user_id] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_profile_fields_data',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_profile_fields_data'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_profile_fields_lang'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_profile_fields_lang'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_profile_fields_lang]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_profile_fields_lang]
(
   [field_id] int  NOT NULL,
   [lang_id] int  NOT NULL,
   [option_id] int  NOT NULL,
   [field_type] smallint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_value] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_profile_fields_lang',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_profile_fields_lang'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_profile_lang'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_profile_lang'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_profile_lang]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_profile_lang]
(
   [field_id] int  NOT NULL,
   [lang_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_explain] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_default_value] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_profile_lang',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_profile_lang'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_qa_confirm'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_qa_confirm'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_qa_confirm]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_qa_confirm]
(

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_id] nchar(32)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [confirm_id] nchar(32)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [lang_iso] nvarchar(30)  NOT NULL,
   [question_id] int  NOT NULL,
   [attempts] int  NOT NULL,
   [confirm_type] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_qa_confirm',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_qa_confirm'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_ranks'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_ranks'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_ranks]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_ranks]
(
   [rank_id] int IDENTITY(13, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [rank_title] nvarchar(255)  NOT NULL,
   [rank_min] int  NOT NULL,
   [rank_special] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [rank_image] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_ranks',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_ranks'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_recycle_bin'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_recycle_bin'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_recycle_bin]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_recycle_bin]
(
   [type] int  NOT NULL,
   [id] int  NOT NULL,
   [content] varbinary(max)  NULL,
   [delete_time] bigint  NOT NULL,
   [delete_user] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_recycle_bin',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_recycle_bin'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_reports'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_reports'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_reports]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_reports]
(
   [report_id] int IDENTITY(723, 1)  NOT NULL,
   [reason_id] int  NOT NULL,
   [post_id] int  NOT NULL,
   [pm_id] int  NOT NULL,
   [user_id] int  NOT NULL,
   [user_notify] tinyint  NOT NULL,
   [report_closed] tinyint  NOT NULL,
   [report_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [report_text] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_reports',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_reports'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_reports_reasons'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_reports_reasons'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_reports_reasons]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_reports_reasons]
(
   [reason_id] int IDENTITY(9, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [reason_title] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [reason_description] nvarchar(max)  NOT NULL,
   [reason_order] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_reports_reasons',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_reports_reasons'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_search_results'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_search_results'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_search_results]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_search_results]
(

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [search_key] nvarchar(32)  NOT NULL,
   [search_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [search_keywords] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [search_authors] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_search_results',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_search_results'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_search_wordlist'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_search_wordlist'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_search_wordlist]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_search_wordlist]
(
   [word_id] int IDENTITY(866, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [word_text] nvarchar(255)  NOT NULL,
   [word_common] tinyint  NOT NULL,
   [word_count] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_search_wordlist',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_search_wordlist'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_search_wordmatch'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_search_wordmatch'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_search_wordmatch]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_search_wordmatch]
(
   [post_id] int  NOT NULL,
   [word_id] int  NOT NULL,
   [title_match] tinyint  NOT NULL,
   [id] bigint IDENTITY(1089, 1)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_search_wordmatch',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_search_wordmatch'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_sessions'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_sessions'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_sessions]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_sessions]
(

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_id] nvarchar(32)  NOT NULL,
   [session_user_id] int  NOT NULL,
   [session_forum_id] int  NOT NULL,
   [session_last_visit] bigint  NOT NULL,
   [session_start] bigint  NOT NULL,
   [session_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_ip] nvarchar(40)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_browser] nvarchar(150)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_forwarded_for] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [session_page] nvarchar(255)  NOT NULL,
   [session_viewonline] tinyint  NOT NULL,
   [session_autologin] tinyint  NOT NULL,
   [session_admin] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_sessions',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_sessions'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_sessions_keys'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_sessions_keys'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_sessions_keys]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_sessions_keys]
(

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [key_id] nvarchar(32)  NOT NULL,
   [user_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [last_ip] nvarchar(40)  NOT NULL,
   [last_login] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_sessions_keys',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_sessions_keys'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_shortcuts'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_shortcuts'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_shortcuts]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_shortcuts]
(
   [topic_id] int  NOT NULL,
   [forum_id] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_shortcuts',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_shortcuts'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_sitelist'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_sitelist'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_sitelist]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_sitelist]
(
   [site_id] int IDENTITY(3, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [site_ip] nvarchar(40)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [site_hostname] nvarchar(255)  NOT NULL,
   [ip_exclude] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_sitelist',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_sitelist'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_smilies'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_smilies'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_smilies]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_smilies]
(
   [smiley_id] int IDENTITY(101, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [code] nvarchar(50)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [emotion] nvarchar(50)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [smiley_url] nvarchar(50)  NOT NULL,
   [smiley_width] int  NOT NULL,
   [smiley_height] int  NOT NULL,
   [smiley_order] int  NOT NULL,
   [display_on_posting] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_smilies',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_smilies'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_styles'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_styles]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_styles]
(
   [style_id] int IDENTITY(10, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [style_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [style_copyright] nvarchar(255)  NOT NULL,
   [style_active] tinyint  NOT NULL,
   [template_id] int  NOT NULL,
   [theme_id] int  NOT NULL,
   [imageset_id] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_styles',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_styles'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_imageset'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_styles_imageset'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_styles_imageset]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_styles_imageset]
(
   [imageset_id] int IDENTITY(9, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [imageset_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [imageset_copyright] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [imageset_path] nvarchar(100)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_styles_imageset',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_styles_imageset'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_imageset_data'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_styles_imageset_data'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_styles_imageset_data]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_styles_imageset_data]
(
   [image_id] int IDENTITY(1599, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [image_name] nvarchar(200)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [image_filename] nvarchar(200)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [image_lang] nvarchar(30)  NOT NULL,
   [image_height] int  NOT NULL,
   [image_width] int  NOT NULL,
   [imageset_id] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_styles_imageset_data',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_styles_imageset_data'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_template'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_styles_template'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_styles_template]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_styles_template]
(
   [template_id] int IDENTITY(9, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_copyright] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_path] nvarchar(100)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [bbcode_bitfield] nvarchar(255)  NOT NULL,
   [template_storedb] tinyint  NOT NULL,
   [template_inherits_id] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_inherit_path] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_styles_template',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_styles_template'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_template_data'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_styles_template_data'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_styles_template_data]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_styles_template_data]
(
   [template_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_filename] nvarchar(100)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_included] nvarchar(max)  NOT NULL,
   [template_mtime] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [template_data] nvarchar(max)  NOT NULL,
   [id] int IDENTITY(1, 1)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_styles_template_data',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_styles_template_data'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_theme'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_styles_theme'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_styles_theme]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_styles_theme]
(
   [theme_id] int IDENTITY(9, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [theme_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [theme_copyright] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [theme_path] nvarchar(100)  NOT NULL,
   [theme_storedb] tinyint  NOT NULL,
   [theme_mtime] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [theme_data] nvarchar(max)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_styles_theme',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_styles_theme'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_topics]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_topics]
(
   [topic_id] int IDENTITY(6678, 1)  NOT NULL,
   [forum_id] int  NOT NULL,
   [icon_id] int  NOT NULL,
   [topic_attachment] tinyint  NOT NULL,
   [topic_approved] tinyint  NOT NULL,
   [topic_reported] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_unicode_ci.
   */

   [topic_title] nvarchar(255)  NOT NULL,
   [topic_poster] int  NOT NULL,
   [topic_time] bigint  NOT NULL,
   [topic_time_limit] bigint  NOT NULL,
   [topic_views] int  NOT NULL,
   [topic_replies] int  NOT NULL,
   [topic_replies_real] int  NOT NULL,
   [topic_status] smallint  NOT NULL,
   [topic_type] smallint  NOT NULL,
   [topic_first_post_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [topic_first_poster_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [topic_first_poster_colour] nvarchar(6)  NOT NULL,
   [topic_last_post_id] int  NOT NULL,
   [topic_last_poster_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [topic_last_poster_name] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [topic_last_poster_colour] nvarchar(6)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [topic_last_post_subject] nvarchar(255)  NOT NULL,
   [topic_last_post_time] bigint  NOT NULL,
   [topic_last_view_time] bigint  NOT NULL,
   [topic_moved_id] int  NOT NULL,
   [topic_bumped] tinyint  NOT NULL,
   [topic_bumper] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [poll_title] nvarchar(255)  NOT NULL,
   [poll_start] bigint  NOT NULL,
   [poll_length] bigint  NOT NULL,
   [poll_max_options] smallint  NOT NULL,
   [poll_last_vote] bigint  NOT NULL,
   [poll_vote_change] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_topics',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_topics'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_topics_posted'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_topics_posted'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_topics_posted]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_topics_posted]
(
   [user_id] int  NOT NULL,
   [topic_id] int  NOT NULL,
   [topic_posted] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_topics_posted',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_topics_posted'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_topics_track'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_topics_track'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_topics_track]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_topics_track]
(
   [user_id] int  NOT NULL,
   [topic_id] int  NOT NULL,
   [forum_id] int  NOT NULL,
   [mark_time] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_topics_track',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_topics_track'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_topics_watch'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_topics_watch'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_topics_watch]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_topics_watch]
(
   [topic_id] int  NOT NULL,
   [user_id] int  NOT NULL,
   [notify_status] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_topics_watch',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_topics_watch'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_user_group'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_user_group'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_user_group]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_user_group]
(
   [group_id] int  NOT NULL,
   [user_id] int  NOT NULL,
   [group_leader] tinyint  NOT NULL,
   [user_pending] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_user_group',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_user_group'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_user_topic_post_number'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_user_topic_post_number'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_user_topic_post_number]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_user_topic_post_number]
(
   [user_id] int  NOT NULL,
   [topic_id] int  NOT NULL,
   [post_no] int  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_user_topic_post_number',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_user_topic_post_number'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_users'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_users'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_users]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_users]
(
   [user_id] int IDENTITY(13605, 1)  NOT NULL,
   [user_type] smallint  NOT NULL,
   [group_id] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_permissions] nvarchar(max)  NOT NULL,
   [user_perm_from] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_ip] nvarchar(40)  NOT NULL,
   [user_regdate] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [username] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [username_clean] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_password] nvarchar(255)  NOT NULL,
   [user_passchg] bigint  NOT NULL,
   [user_pass_convert] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_email] nvarchar(100)  NOT NULL,
   [user_email_hash] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_birthday] nvarchar(10)  NOT NULL,
   [user_lastvisit] bigint  NOT NULL,
   [user_lastmark] bigint  NOT NULL,
   [user_lastpost_time] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_lastpage] nvarchar(200)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_last_confirm_key] nvarchar(10)  NOT NULL,
   [user_last_search] bigint  NOT NULL,
   [user_warnings] smallint  NOT NULL,
   [user_last_warning] bigint  NOT NULL,
   [user_login_attempts] smallint  NOT NULL,
   [user_inactive_reason] smallint  NOT NULL,
   [user_inactive_time] bigint  NOT NULL,
   [user_posts] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_lang] nvarchar(30)  NOT NULL,
   [user_timezone] decimal(5, 2)  NOT NULL,
   [user_dst] tinyint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_dateformat] nvarchar(30)  NOT NULL,
   [user_style] int  NOT NULL,
   [user_rank] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_colour] nvarchar(6)  NOT NULL,
   [user_new_privmsg] int  NOT NULL,
   [user_unread_privmsg] int  NOT NULL,
   [user_last_privmsg] bigint  NOT NULL,
   [user_message_rules] tinyint  NOT NULL,
   [user_full_folder] int  NOT NULL,
   [user_emailtime] bigint  NOT NULL,
   [user_topic_show_days] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_topic_sortby_type] nchar(1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_topic_sortby_dir] nchar(1)  NOT NULL,
   [user_post_show_days] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_post_sortby_type] nchar(1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_post_sortby_dir] nchar(1)  NOT NULL,
   [user_notify] tinyint  NOT NULL,
   [user_notify_pm] tinyint  NOT NULL,
   [user_notify_type] smallint  NOT NULL,
   [user_allow_pm] tinyint  NOT NULL,
   [user_allow_viewonline] tinyint  NOT NULL,
   [user_allow_viewemail] tinyint  NOT NULL,
   [user_allow_massemail] tinyint  NOT NULL,
   [user_options] bigint  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_avatar] nvarchar(255)  NOT NULL,
   [user_avatar_type] smallint  NOT NULL,
   [user_avatar_width] int  NOT NULL,
   [user_avatar_height] int  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_sig] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_sig_bbcode_uid] nvarchar(8)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_sig_bbcode_bitfield] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_from] nvarchar(100)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_icq] nvarchar(15)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_aim] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_yim] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_msnm] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_jabber] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_website] nvarchar(200)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_occ] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_interests] nvarchar(max)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_actkey] nvarchar(32)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_newpasswd] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [user_form_salt] nvarchar(32)  NOT NULL,
   [user_new] tinyint  NOT NULL,
   [user_reminded] smallint  NOT NULL,
   [user_reminded_time] bigint  NOT NULL,
   [user_edit_time] bigint  NOT NULL,
   [jump_to_unread] smallint  NULL,
   [user_should_sign_in] smallint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_users',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_users'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_warnings'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_warnings'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_warnings]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_warnings]
(
   [warning_id] int IDENTITY(1, 1)  NOT NULL,
   [user_id] int  NOT NULL,
   [post_id] int  NOT NULL,
   [log_id] int  NOT NULL,
   [warning_time] bigint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_warnings',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_warnings'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_words'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_words'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_words]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_words]
(
   [word_id] int IDENTITY(106, 1)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [word] nvarchar(255)  NOT NULL,

   /*
   *   SSMA warning messages:
   *   M2SS0183: The following SQL clause was ignored during conversion: COLLATE utf8_bin.
   */

   [replacement] nvarchar(255)  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_words',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_words'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_zebra'  AND sc.name = N'dbo'  AND type in (N'U'))
BEGIN

  DECLARE @drop_statement nvarchar(500)

  DECLARE drop_cursor CURSOR FOR
      SELECT 'alter table '+quotename(schema_name(ob.schema_id))+
      '.'+quotename(object_name(ob.object_id))+ ' drop constraint ' + quotename(fk.name) 
      FROM sys.objects ob INNER JOIN sys.foreign_keys fk ON fk.parent_object_id = ob.object_id
      WHERE fk.referenced_object_id = 
          (
             SELECT so.object_id 
             FROM sys.objects so JOIN sys.schemas sc
             ON so.schema_id = sc.schema_id
             WHERE so.name = N'phpbb_zebra'  AND sc.name = N'dbo'  AND type in (N'U')
           )

  OPEN drop_cursor

  FETCH NEXT FROM drop_cursor
  INTO @drop_statement

  WHILE @@FETCH_STATUS = 0
  BEGIN
     EXEC (@drop_statement)

     FETCH NEXT FROM drop_cursor
     INTO @drop_statement
  END

  CLOSE drop_cursor
  DEALLOCATE drop_cursor

  DROP TABLE [dbo].[phpbb_zebra]
END 
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE 
[dbo].[phpbb_zebra]
(
   [user_id] int  NOT NULL,
   [zebra_id] int  NOT NULL,
   [friend] tinyint  NOT NULL,
   [foe] tinyint  NOT NULL
)
WITH (DATA_COMPRESSION = NONE)
GO
BEGIN TRY
    EXEC sp_addextendedproperty
        N'MS_SSMA_SOURCE', N'`dbo`.phpbb_zebra',
        N'SCHEMA', N'dbo',
        N'TABLE', N'phpbb_zebra'
END TRY
BEGIN CATCH
    IF (@@TRANCOUNT > 0) ROLLBACK
    PRINT ERROR_MESSAGE()
END CATCH
GO
IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_acl_groups_group_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_acl_groups] DROP CONSTRAINT [PK_phpbb_acl_groups_group_id]
 GO



ALTER TABLE [dbo].[phpbb_acl_groups]
 ADD CONSTRAINT [PK_phpbb_acl_groups_group_id]
   PRIMARY KEY
   CLUSTERED ([group_id] ASC, [forum_id] ASC, [auth_option_id] ASC, [auth_role_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_acl_options_auth_option_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_acl_options] DROP CONSTRAINT [PK_phpbb_acl_options_auth_option_id]
 GO



ALTER TABLE [dbo].[phpbb_acl_options]
 ADD CONSTRAINT [PK_phpbb_acl_options_auth_option_id]
   PRIMARY KEY
   CLUSTERED ([auth_option_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_acl_roles_role_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_acl_roles] DROP CONSTRAINT [PK_phpbb_acl_roles_role_id]
 GO



ALTER TABLE [dbo].[phpbb_acl_roles]
 ADD CONSTRAINT [PK_phpbb_acl_roles_role_id]
   PRIMARY KEY
   CLUSTERED ([role_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_acl_roles_data_role_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_acl_roles_data] DROP CONSTRAINT [PK_phpbb_acl_roles_data_role_id]
 GO



ALTER TABLE [dbo].[phpbb_acl_roles_data]
 ADD CONSTRAINT [PK_phpbb_acl_roles_data_role_id]
   PRIMARY KEY
   CLUSTERED ([role_id] ASC, [auth_option_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_acl_users_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_acl_users] DROP CONSTRAINT [PK_phpbb_acl_users_user_id]
 GO



ALTER TABLE [dbo].[phpbb_acl_users]
 ADD CONSTRAINT [PK_phpbb_acl_users_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC, [forum_id] ASC, [auth_option_id] ASC, [auth_role_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_attachments_attach_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_attachments] DROP CONSTRAINT [PK_phpbb_attachments_attach_id]
 GO



ALTER TABLE [dbo].[phpbb_attachments]
 ADD CONSTRAINT [PK_phpbb_attachments_attach_id]
   PRIMARY KEY
   CLUSTERED ([attach_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_banlist_ban_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_banlist] DROP CONSTRAINT [PK_phpbb_banlist_ban_id]
 GO



ALTER TABLE [dbo].[phpbb_banlist]
 ADD CONSTRAINT [PK_phpbb_banlist_ban_id]
   PRIMARY KEY
   CLUSTERED ([ban_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_bbcodes_bbcode_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_bbcodes] DROP CONSTRAINT [PK_phpbb_bbcodes_bbcode_id]
 GO



ALTER TABLE [dbo].[phpbb_bbcodes]
 ADD CONSTRAINT [PK_phpbb_bbcodes_bbcode_id]
   PRIMARY KEY
   CLUSTERED ([bbcode_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_bookmarks_topic_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_bookmarks] DROP CONSTRAINT [PK_phpbb_bookmarks_topic_id]
 GO



ALTER TABLE [dbo].[phpbb_bookmarks]
 ADD CONSTRAINT [PK_phpbb_bookmarks_topic_id]
   PRIMARY KEY
   CLUSTERED ([topic_id] ASC, [user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_bots_bot_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_bots] DROP CONSTRAINT [PK_phpbb_bots_bot_id]
 GO



ALTER TABLE [dbo].[phpbb_bots]
 ADD CONSTRAINT [PK_phpbb_bots_bot_id]
   PRIMARY KEY
   CLUSTERED ([bot_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_captcha_answers_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_captcha_answers] DROP CONSTRAINT [PK_phpbb_captcha_answers_id]
 GO



ALTER TABLE [dbo].[phpbb_captcha_answers]
 ADD CONSTRAINT [PK_phpbb_captcha_answers_id]
   PRIMARY KEY
   CLUSTERED ([id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_captcha_questions_question_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_captcha_questions] DROP CONSTRAINT [PK_phpbb_captcha_questions_question_id]
 GO



ALTER TABLE [dbo].[phpbb_captcha_questions]
 ADD CONSTRAINT [PK_phpbb_captcha_questions_question_id]
   PRIMARY KEY
   CLUSTERED ([question_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_config_config_name'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_config] DROP CONSTRAINT [PK_phpbb_config_config_name]
 GO



ALTER TABLE [dbo].[phpbb_config]
 ADD CONSTRAINT [PK_phpbb_config_config_name]
   PRIMARY KEY
   CLUSTERED ([config_name] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_confirm_session_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_confirm] DROP CONSTRAINT [PK_phpbb_confirm_session_id]
 GO



ALTER TABLE [dbo].[phpbb_confirm]
 ADD CONSTRAINT [PK_phpbb_confirm_session_id]
   PRIMARY KEY
   CLUSTERED ([session_id] ASC, [confirm_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_disallow_disallow_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_disallow] DROP CONSTRAINT [PK_phpbb_disallow_disallow_id]
 GO



ALTER TABLE [dbo].[phpbb_disallow]
 ADD CONSTRAINT [PK_phpbb_disallow_disallow_id]
   PRIMARY KEY
   CLUSTERED ([disallow_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_drafts_draft_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_drafts] DROP CONSTRAINT [PK_phpbb_drafts_draft_id]
 GO



ALTER TABLE [dbo].[phpbb_drafts]
 ADD CONSTRAINT [PK_phpbb_drafts_draft_id]
   PRIMARY KEY
   CLUSTERED ([draft_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_extension_groups_group_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_extension_groups] DROP CONSTRAINT [PK_phpbb_extension_groups_group_id]
 GO



ALTER TABLE [dbo].[phpbb_extension_groups]
 ADD CONSTRAINT [PK_phpbb_extension_groups_group_id]
   PRIMARY KEY
   CLUSTERED ([group_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_extensions_extension_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_extensions] DROP CONSTRAINT [PK_phpbb_extensions_extension_id]
 GO



ALTER TABLE [dbo].[phpbb_extensions]
 ADD CONSTRAINT [PK_phpbb_extensions_extension_id]
   PRIMARY KEY
   CLUSTERED ([extension_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_forums_forum_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_forums] DROP CONSTRAINT [PK_phpbb_forums_forum_id]
 GO



ALTER TABLE [dbo].[phpbb_forums]
 ADD CONSTRAINT [PK_phpbb_forums_forum_id]
   PRIMARY KEY
   CLUSTERED ([forum_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_forums_access_forum_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_forums_access] DROP CONSTRAINT [PK_phpbb_forums_access_forum_id]
 GO



ALTER TABLE [dbo].[phpbb_forums_access]
 ADD CONSTRAINT [PK_phpbb_forums_access_forum_id]
   PRIMARY KEY
   CLUSTERED ([forum_id] ASC, [user_id] ASC, [session_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_forums_track_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_forums_track] DROP CONSTRAINT [PK_phpbb_forums_track_user_id]
 GO



ALTER TABLE [dbo].[phpbb_forums_track]
 ADD CONSTRAINT [PK_phpbb_forums_track_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC, [forum_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_forums_watch_forum_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_forums_watch] DROP CONSTRAINT [PK_phpbb_forums_watch_forum_id]
 GO



ALTER TABLE [dbo].[phpbb_forums_watch]
 ADD CONSTRAINT [PK_phpbb_forums_watch_forum_id]
   PRIMARY KEY
   CLUSTERED ([forum_id] ASC, [user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_groups_group_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_groups] DROP CONSTRAINT [PK_phpbb_groups_group_id]
 GO



ALTER TABLE [dbo].[phpbb_groups]
 ADD CONSTRAINT [PK_phpbb_groups_group_id]
   PRIMARY KEY
   CLUSTERED ([group_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_icons_icons_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_icons] DROP CONSTRAINT [PK_phpbb_icons_icons_id]
 GO



ALTER TABLE [dbo].[phpbb_icons]
 ADD CONSTRAINT [PK_phpbb_icons_icons_id]
   PRIMARY KEY
   CLUSTERED ([icons_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_lang_lang_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_lang] DROP CONSTRAINT [PK_phpbb_lang_lang_id]
 GO



ALTER TABLE [dbo].[phpbb_lang]
 ADD CONSTRAINT [PK_phpbb_lang_lang_id]
   PRIMARY KEY
   CLUSTERED ([lang_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_log_log_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_log] DROP CONSTRAINT [PK_phpbb_log_log_id]
 GO



ALTER TABLE [dbo].[phpbb_log]
 ADD CONSTRAINT [PK_phpbb_log_log_id]
   PRIMARY KEY
   CLUSTERED ([log_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_moderator_cache_forum_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_moderator_cache] DROP CONSTRAINT [PK_phpbb_moderator_cache_forum_id]
 GO



ALTER TABLE [dbo].[phpbb_moderator_cache]
 ADD CONSTRAINT [PK_phpbb_moderator_cache_forum_id]
   PRIMARY KEY
   CLUSTERED ([forum_id] ASC, [user_id] ASC, [group_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_modules_module_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_modules] DROP CONSTRAINT [PK_phpbb_modules_module_id]
 GO



ALTER TABLE [dbo].[phpbb_modules]
 ADD CONSTRAINT [PK_phpbb_modules_module_id]
   PRIMARY KEY
   CLUSTERED ([module_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_posts_post_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_posts] DROP CONSTRAINT [PK_phpbb_posts_post_id]
 GO



ALTER TABLE [dbo].[phpbb_posts]
 ADD CONSTRAINT [PK_phpbb_posts_post_id]
   PRIMARY KEY
   CLUSTERED ([post_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_privmsgs_msg_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_privmsgs] DROP CONSTRAINT [PK_phpbb_privmsgs_msg_id]
 GO



ALTER TABLE [dbo].[phpbb_privmsgs]
 ADD CONSTRAINT [PK_phpbb_privmsgs_msg_id]
   PRIMARY KEY
   CLUSTERED ([msg_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_privmsgs_folder_folder_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_privmsgs_folder] DROP CONSTRAINT [PK_phpbb_privmsgs_folder_folder_id]
 GO



ALTER TABLE [dbo].[phpbb_privmsgs_folder]
 ADD CONSTRAINT [PK_phpbb_privmsgs_folder_folder_id]
   PRIMARY KEY
   CLUSTERED ([folder_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_privmsgs_rules_rule_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_privmsgs_rules] DROP CONSTRAINT [PK_phpbb_privmsgs_rules_rule_id]
 GO



ALTER TABLE [dbo].[phpbb_privmsgs_rules]
 ADD CONSTRAINT [PK_phpbb_privmsgs_rules_rule_id]
   PRIMARY KEY
   CLUSTERED ([rule_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_privmsgs_to_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_privmsgs_to] DROP CONSTRAINT [PK_phpbb_privmsgs_to_id]
 GO



ALTER TABLE [dbo].[phpbb_privmsgs_to]
 ADD CONSTRAINT [PK_phpbb_privmsgs_to_id]
   PRIMARY KEY
   CLUSTERED ([id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_profile_fields_field_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_profile_fields] DROP CONSTRAINT [PK_phpbb_profile_fields_field_id]
 GO



ALTER TABLE [dbo].[phpbb_profile_fields]
 ADD CONSTRAINT [PK_phpbb_profile_fields_field_id]
   PRIMARY KEY
   CLUSTERED ([field_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_profile_fields_data_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_profile_fields_data] DROP CONSTRAINT [PK_phpbb_profile_fields_data_user_id]
 GO



ALTER TABLE [dbo].[phpbb_profile_fields_data]
 ADD CONSTRAINT [PK_phpbb_profile_fields_data_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_profile_fields_lang_field_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_profile_fields_lang] DROP CONSTRAINT [PK_phpbb_profile_fields_lang_field_id]
 GO



ALTER TABLE [dbo].[phpbb_profile_fields_lang]
 ADD CONSTRAINT [PK_phpbb_profile_fields_lang_field_id]
   PRIMARY KEY
   CLUSTERED ([field_id] ASC, [lang_id] ASC, [option_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_profile_lang_field_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_profile_lang] DROP CONSTRAINT [PK_phpbb_profile_lang_field_id]
 GO



ALTER TABLE [dbo].[phpbb_profile_lang]
 ADD CONSTRAINT [PK_phpbb_profile_lang_field_id]
   PRIMARY KEY
   CLUSTERED ([field_id] ASC, [lang_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_qa_confirm_confirm_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_qa_confirm] DROP CONSTRAINT [PK_phpbb_qa_confirm_confirm_id]
 GO



ALTER TABLE [dbo].[phpbb_qa_confirm]
 ADD CONSTRAINT [PK_phpbb_qa_confirm_confirm_id]
   PRIMARY KEY
   CLUSTERED ([confirm_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_ranks_rank_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_ranks] DROP CONSTRAINT [PK_phpbb_ranks_rank_id]
 GO



ALTER TABLE [dbo].[phpbb_ranks]
 ADD CONSTRAINT [PK_phpbb_ranks_rank_id]
   PRIMARY KEY
   CLUSTERED ([rank_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_recycle_bin_type'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_recycle_bin] DROP CONSTRAINT [PK_phpbb_recycle_bin_type]
 GO



ALTER TABLE [dbo].[phpbb_recycle_bin]
 ADD CONSTRAINT [PK_phpbb_recycle_bin_type]
   PRIMARY KEY
   CLUSTERED ([type] ASC, [id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_reports_report_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_reports] DROP CONSTRAINT [PK_phpbb_reports_report_id]
 GO



ALTER TABLE [dbo].[phpbb_reports]
 ADD CONSTRAINT [PK_phpbb_reports_report_id]
   PRIMARY KEY
   CLUSTERED ([report_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_reports_reasons_reason_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_reports_reasons] DROP CONSTRAINT [PK_phpbb_reports_reasons_reason_id]
 GO



ALTER TABLE [dbo].[phpbb_reports_reasons]
 ADD CONSTRAINT [PK_phpbb_reports_reasons_reason_id]
   PRIMARY KEY
   CLUSTERED ([reason_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_search_results_search_key'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_search_results] DROP CONSTRAINT [PK_phpbb_search_results_search_key]
 GO



ALTER TABLE [dbo].[phpbb_search_results]
 ADD CONSTRAINT [PK_phpbb_search_results_search_key]
   PRIMARY KEY
   CLUSTERED ([search_key] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_search_wordlist_word_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_search_wordlist] DROP CONSTRAINT [PK_phpbb_search_wordlist_word_id]
 GO



ALTER TABLE [dbo].[phpbb_search_wordlist]
 ADD CONSTRAINT [PK_phpbb_search_wordlist_word_id]
   PRIMARY KEY
   CLUSTERED ([word_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_search_wordmatch_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_search_wordmatch] DROP CONSTRAINT [PK_phpbb_search_wordmatch_id]
 GO



ALTER TABLE [dbo].[phpbb_search_wordmatch]
 ADD CONSTRAINT [PK_phpbb_search_wordmatch_id]
   PRIMARY KEY
   CLUSTERED ([id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_sessions_session_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_sessions] DROP CONSTRAINT [PK_phpbb_sessions_session_id]
 GO



ALTER TABLE [dbo].[phpbb_sessions]
 ADD CONSTRAINT [PK_phpbb_sessions_session_id]
   PRIMARY KEY
   CLUSTERED ([session_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_sessions_keys_key_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_sessions_keys] DROP CONSTRAINT [PK_phpbb_sessions_keys_key_id]
 GO



ALTER TABLE [dbo].[phpbb_sessions_keys]
 ADD CONSTRAINT [PK_phpbb_sessions_keys_key_id]
   PRIMARY KEY
   CLUSTERED ([key_id] ASC, [user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_shortcuts_topic_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_shortcuts] DROP CONSTRAINT [PK_phpbb_shortcuts_topic_id]
 GO



ALTER TABLE [dbo].[phpbb_shortcuts]
 ADD CONSTRAINT [PK_phpbb_shortcuts_topic_id]
   PRIMARY KEY
   CLUSTERED ([topic_id] ASC, [forum_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_sitelist_site_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_sitelist] DROP CONSTRAINT [PK_phpbb_sitelist_site_id]
 GO



ALTER TABLE [dbo].[phpbb_sitelist]
 ADD CONSTRAINT [PK_phpbb_sitelist_site_id]
   PRIMARY KEY
   CLUSTERED ([site_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_smilies_smiley_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_smilies] DROP CONSTRAINT [PK_phpbb_smilies_smiley_id]
 GO



ALTER TABLE [dbo].[phpbb_smilies]
 ADD CONSTRAINT [PK_phpbb_smilies_smiley_id]
   PRIMARY KEY
   CLUSTERED ([smiley_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_styles_style_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_styles] DROP CONSTRAINT [PK_phpbb_styles_style_id]
 GO



ALTER TABLE [dbo].[phpbb_styles]
 ADD CONSTRAINT [PK_phpbb_styles_style_id]
   PRIMARY KEY
   CLUSTERED ([style_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_styles_imageset_imageset_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_styles_imageset] DROP CONSTRAINT [PK_phpbb_styles_imageset_imageset_id]
 GO



ALTER TABLE [dbo].[phpbb_styles_imageset]
 ADD CONSTRAINT [PK_phpbb_styles_imageset_imageset_id]
   PRIMARY KEY
   CLUSTERED ([imageset_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_styles_imageset_data_image_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_styles_imageset_data] DROP CONSTRAINT [PK_phpbb_styles_imageset_data_image_id]
 GO



ALTER TABLE [dbo].[phpbb_styles_imageset_data]
 ADD CONSTRAINT [PK_phpbb_styles_imageset_data_image_id]
   PRIMARY KEY
   CLUSTERED ([image_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_styles_template_template_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_styles_template] DROP CONSTRAINT [PK_phpbb_styles_template_template_id]
 GO



ALTER TABLE [dbo].[phpbb_styles_template]
 ADD CONSTRAINT [PK_phpbb_styles_template_template_id]
   PRIMARY KEY
   CLUSTERED ([template_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_styles_template_data_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_styles_template_data] DROP CONSTRAINT [PK_phpbb_styles_template_data_id]
 GO



ALTER TABLE [dbo].[phpbb_styles_template_data]
 ADD CONSTRAINT [PK_phpbb_styles_template_data_id]
   PRIMARY KEY
   CLUSTERED ([id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_styles_theme_theme_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_styles_theme] DROP CONSTRAINT [PK_phpbb_styles_theme_theme_id]
 GO



ALTER TABLE [dbo].[phpbb_styles_theme]
 ADD CONSTRAINT [PK_phpbb_styles_theme_theme_id]
   PRIMARY KEY
   CLUSTERED ([theme_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_topics_topic_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_topics] DROP CONSTRAINT [PK_phpbb_topics_topic_id]
 GO



ALTER TABLE [dbo].[phpbb_topics]
 ADD CONSTRAINT [PK_phpbb_topics_topic_id]
   PRIMARY KEY
   CLUSTERED ([topic_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_topics_posted_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_topics_posted] DROP CONSTRAINT [PK_phpbb_topics_posted_user_id]
 GO



ALTER TABLE [dbo].[phpbb_topics_posted]
 ADD CONSTRAINT [PK_phpbb_topics_posted_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC, [topic_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_topics_track_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_topics_track] DROP CONSTRAINT [PK_phpbb_topics_track_user_id]
 GO



ALTER TABLE [dbo].[phpbb_topics_track]
 ADD CONSTRAINT [PK_phpbb_topics_track_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC, [topic_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_topics_watch_topic_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_topics_watch] DROP CONSTRAINT [PK_phpbb_topics_watch_topic_id]
 GO



ALTER TABLE [dbo].[phpbb_topics_watch]
 ADD CONSTRAINT [PK_phpbb_topics_watch_topic_id]
   PRIMARY KEY
   CLUSTERED ([topic_id] ASC, [user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_user_group_group_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_user_group] DROP CONSTRAINT [PK_phpbb_user_group_group_id]
 GO



ALTER TABLE [dbo].[phpbb_user_group]
 ADD CONSTRAINT [PK_phpbb_user_group_group_id]
   PRIMARY KEY
   CLUSTERED ([group_id] ASC, [user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_user_topic_post_number_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_user_topic_post_number] DROP CONSTRAINT [PK_phpbb_user_topic_post_number_user_id]
 GO



ALTER TABLE [dbo].[phpbb_user_topic_post_number]
 ADD CONSTRAINT [PK_phpbb_user_topic_post_number_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC, [topic_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_users_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_users] DROP CONSTRAINT [PK_phpbb_users_user_id]
 GO



ALTER TABLE [dbo].[phpbb_users]
 ADD CONSTRAINT [PK_phpbb_users_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_warnings_warning_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_warnings] DROP CONSTRAINT [PK_phpbb_warnings_warning_id]
 GO



ALTER TABLE [dbo].[phpbb_warnings]
 ADD CONSTRAINT [PK_phpbb_warnings_warning_id]
   PRIMARY KEY
   CLUSTERED ([warning_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_words_word_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_words] DROP CONSTRAINT [PK_phpbb_words_word_id]
 GO



ALTER TABLE [dbo].[phpbb_words]
 ADD CONSTRAINT [PK_phpbb_words_word_id]
   PRIMARY KEY
   CLUSTERED ([word_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'PK_phpbb_zebra_user_id'  AND sc.name = N'dbo'  AND type in (N'PK'))
ALTER TABLE [dbo].[phpbb_zebra] DROP CONSTRAINT [PK_phpbb_zebra_user_id]
 GO



ALTER TABLE [dbo].[phpbb_zebra]
 ADD CONSTRAINT [PK_phpbb_zebra_user_id]
   PRIMARY KEY
   CLUSTERED ([user_id] ASC, [zebra_id] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_acl_options$auth_option'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_acl_options] DROP CONSTRAINT [phpbb_acl_options$auth_option]
 GO



ALTER TABLE [dbo].[phpbb_acl_options]
 ADD CONSTRAINT [phpbb_acl_options$auth_option]
 UNIQUE 
   NONCLUSTERED ([auth_option] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_search_wordlist$wrd_txt'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_search_wordlist] DROP CONSTRAINT [phpbb_search_wordlist$wrd_txt]
 GO



ALTER TABLE [dbo].[phpbb_search_wordlist]
 ADD CONSTRAINT [phpbb_search_wordlist$wrd_txt]
 UNIQUE 
   NONCLUSTERED ([word_text] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_search_wordmatch$unq_mtch'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_search_wordmatch] DROP CONSTRAINT [phpbb_search_wordmatch$unq_mtch]
 GO



ALTER TABLE [dbo].[phpbb_search_wordmatch]
 ADD CONSTRAINT [phpbb_search_wordmatch$unq_mtch]
 UNIQUE 
   NONCLUSTERED ([word_id] ASC, [post_id] ASC, [title_match] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles$style_name'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_styles] DROP CONSTRAINT [phpbb_styles$style_name]
 GO



ALTER TABLE [dbo].[phpbb_styles]
 ADD CONSTRAINT [phpbb_styles$style_name]
 UNIQUE 
   NONCLUSTERED ([style_name] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_imageset$imgset_nm'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_styles_imageset] DROP CONSTRAINT [phpbb_styles_imageset$imgset_nm]
 GO



ALTER TABLE [dbo].[phpbb_styles_imageset]
 ADD CONSTRAINT [phpbb_styles_imageset$imgset_nm]
 UNIQUE 
   NONCLUSTERED ([imageset_name] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_template$tmplte_nm'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_styles_template] DROP CONSTRAINT [phpbb_styles_template$tmplte_nm]
 GO



ALTER TABLE [dbo].[phpbb_styles_template]
 ADD CONSTRAINT [phpbb_styles_template$tmplte_nm]
 UNIQUE 
   NONCLUSTERED ([template_name] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_styles_theme$theme_name'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_styles_theme] DROP CONSTRAINT [phpbb_styles_theme$theme_name]
 GO



ALTER TABLE [dbo].[phpbb_styles_theme]
 ADD CONSTRAINT [phpbb_styles_theme$theme_name]
 UNIQUE 
   NONCLUSTERED ([theme_name] ASC)

GO

IF EXISTS (SELECT * FROM sys.objects so JOIN sys.schemas sc ON so.schema_id = sc.schema_id WHERE so.name = N'phpbb_users$username_clean'  AND sc.name = N'dbo'  AND type in (N'UQ'))
ALTER TABLE [dbo].[phpbb_users] DROP CONSTRAINT [phpbb_users$username_clean]
 GO



ALTER TABLE [dbo].[phpbb_users]
 ADD CONSTRAINT [phpbb_users$username_clean]
 UNIQUE 
   NONCLUSTERED ([username_clean] ASC)

GO

IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_roles_data'  AND sc.name = N'dbo'  AND si.name = N'ath_op_id' AND so.type in (N'U'))
   DROP INDEX [ath_op_id] ON [dbo].[phpbb_acl_roles_data] 
GO
CREATE NONCLUSTERED INDEX [ath_op_id] ON [dbo].[phpbb_acl_roles_data]
(
   [auth_option_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_groups'  AND sc.name = N'dbo'  AND si.name = N'auth_opt_id' AND so.type in (N'U'))
   DROP INDEX [auth_opt_id] ON [dbo].[phpbb_acl_groups] 
GO
CREATE NONCLUSTERED INDEX [auth_opt_id] ON [dbo].[phpbb_acl_groups]
(
   [auth_option_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_users'  AND sc.name = N'dbo'  AND si.name = N'auth_option_id' AND so.type in (N'U'))
   DROP INDEX [auth_option_id] ON [dbo].[phpbb_acl_users] 
GO
CREATE NONCLUSTERED INDEX [auth_option_id] ON [dbo].[phpbb_acl_users]
(
   [auth_option_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_groups'  AND sc.name = N'dbo'  AND si.name = N'auth_role_id' AND so.type in (N'U'))
   DROP INDEX [auth_role_id] ON [dbo].[phpbb_acl_groups] 
GO
CREATE NONCLUSTERED INDEX [auth_role_id] ON [dbo].[phpbb_acl_groups]
(
   [auth_role_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_users'  AND sc.name = N'dbo'  AND si.name = N'auth_role_id' AND so.type in (N'U'))
   DROP INDEX [auth_role_id] ON [dbo].[phpbb_acl_users] 
GO
CREATE NONCLUSTERED INDEX [auth_role_id] ON [dbo].[phpbb_acl_users]
(
   [auth_role_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs_to'  AND sc.name = N'dbo'  AND si.name = N'author_id' AND so.type in (N'U'))
   DROP INDEX [author_id] ON [dbo].[phpbb_privmsgs_to] 
GO
CREATE NONCLUSTERED INDEX [author_id] ON [dbo].[phpbb_privmsgs_to]
(
   [author_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs'  AND sc.name = N'dbo'  AND si.name = N'author_id' AND so.type in (N'U'))
   DROP INDEX [author_id] ON [dbo].[phpbb_privmsgs] 
GO
CREATE NONCLUSTERED INDEX [author_id] ON [dbo].[phpbb_privmsgs]
(
   [author_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs'  AND sc.name = N'dbo'  AND si.name = N'author_ip' AND so.type in (N'U'))
   DROP INDEX [author_ip] ON [dbo].[phpbb_privmsgs] 
GO
CREATE NONCLUSTERED INDEX [author_ip] ON [dbo].[phpbb_privmsgs]
(
   [author_ip] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_banlist'  AND sc.name = N'dbo'  AND si.name = N'ban_email' AND so.type in (N'U'))
   DROP INDEX [ban_email] ON [dbo].[phpbb_banlist] 
GO
CREATE NONCLUSTERED INDEX [ban_email] ON [dbo].[phpbb_banlist]
(
   [ban_email] ASC,
   [ban_exclude] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_banlist'  AND sc.name = N'dbo'  AND si.name = N'ban_end' AND so.type in (N'U'))
   DROP INDEX [ban_end] ON [dbo].[phpbb_banlist] 
GO
CREATE NONCLUSTERED INDEX [ban_end] ON [dbo].[phpbb_banlist]
(
   [ban_end] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_banlist'  AND sc.name = N'dbo'  AND si.name = N'ban_ip' AND so.type in (N'U'))
   DROP INDEX [ban_ip] ON [dbo].[phpbb_banlist] 
GO
CREATE NONCLUSTERED INDEX [ban_ip] ON [dbo].[phpbb_banlist]
(
   [ban_ip] ASC,
   [ban_exclude] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_banlist'  AND sc.name = N'dbo'  AND si.name = N'ban_user' AND so.type in (N'U'))
   DROP INDEX [ban_user] ON [dbo].[phpbb_banlist] 
GO
CREATE NONCLUSTERED INDEX [ban_user] ON [dbo].[phpbb_banlist]
(
   [ban_userid] ASC,
   [ban_exclude] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_bots'  AND sc.name = N'dbo'  AND si.name = N'bot_active' AND so.type in (N'U'))
   DROP INDEX [bot_active] ON [dbo].[phpbb_bots] 
GO
CREATE NONCLUSTERED INDEX [bot_active] ON [dbo].[phpbb_bots]
(
   [bot_active] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_modules'  AND sc.name = N'dbo'  AND si.name = N'class_left_id' AND so.type in (N'U'))
   DROP INDEX [class_left_id] ON [dbo].[phpbb_modules] 
GO
CREATE NONCLUSTERED INDEX [class_left_id] ON [dbo].[phpbb_modules]
(
   [module_class] ASC,
   [left_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_confirm'  AND sc.name = N'dbo'  AND si.name = N'confirm_type' AND so.type in (N'U'))
   DROP INDEX [confirm_type] ON [dbo].[phpbb_confirm] 
GO
CREATE NONCLUSTERED INDEX [confirm_type] ON [dbo].[phpbb_confirm]
(
   [confirm_type] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_moderator_cache'  AND sc.name = N'dbo'  AND si.name = N'disp_idx' AND so.type in (N'U'))
   DROP INDEX [disp_idx] ON [dbo].[phpbb_moderator_cache] 
GO
CREATE NONCLUSTERED INDEX [disp_idx] ON [dbo].[phpbb_moderator_cache]
(
   [display_on_index] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_bbcodes'  AND sc.name = N'dbo'  AND si.name = N'display_on_post' AND so.type in (N'U'))
   DROP INDEX [display_on_post] ON [dbo].[phpbb_bbcodes] 
GO
CREATE NONCLUSTERED INDEX [display_on_post] ON [dbo].[phpbb_bbcodes]
(
   [display_on_posting] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_smilies'  AND sc.name = N'dbo'  AND si.name = N'display_on_post' AND so.type in (N'U'))
   DROP INDEX [display_on_post] ON [dbo].[phpbb_smilies] 
GO
CREATE NONCLUSTERED INDEX [display_on_post] ON [dbo].[phpbb_smilies]
(
   [display_on_posting] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_icons'  AND sc.name = N'dbo'  AND si.name = N'display_on_posting' AND so.type in (N'U'))
   DROP INDEX [display_on_posting] ON [dbo].[phpbb_icons] 
GO
CREATE NONCLUSTERED INDEX [display_on_posting] ON [dbo].[phpbb_icons]
(
   [display_on_posting] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND si.name = N'fid_time_moved' AND so.type in (N'U'))
   DROP INDEX [fid_time_moved] ON [dbo].[phpbb_topics] 
GO
CREATE NONCLUSTERED INDEX [fid_time_moved] ON [dbo].[phpbb_topics]
(
   [forum_id] ASC,
   [topic_last_post_time] ASC,
   [topic_moved_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND si.name = N'filetime' AND so.type in (N'U'))
   DROP INDEX [filetime] ON [dbo].[phpbb_attachments] 
GO
CREATE NONCLUSTERED INDEX [filetime] ON [dbo].[phpbb_attachments]
(
   [filetime] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_profile_fields'  AND sc.name = N'dbo'  AND si.name = N'fld_ordr' AND so.type in (N'U'))
   DROP INDEX [fld_ordr] ON [dbo].[phpbb_profile_fields] 
GO
CREATE NONCLUSTERED INDEX [fld_ordr] ON [dbo].[phpbb_profile_fields]
(
   [field_order] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_profile_fields'  AND sc.name = N'dbo'  AND si.name = N'fld_type' AND so.type in (N'U'))
   DROP INDEX [fld_type] ON [dbo].[phpbb_profile_fields] 
GO
CREATE NONCLUSTERED INDEX [fld_type] ON [dbo].[phpbb_profile_fields]
(
   [field_type] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND si.name = N'forum_appr_last' AND so.type in (N'U'))
   DROP INDEX [forum_appr_last] ON [dbo].[phpbb_topics] 
GO
CREATE NONCLUSTERED INDEX [forum_appr_last] ON [dbo].[phpbb_topics]
(
   [forum_id] ASC,
   [topic_approved] ASC,
   [topic_last_post_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND si.name = N'forum_id' AND so.type in (N'U'))
   DROP INDEX [forum_id] ON [dbo].[phpbb_log] 
GO
CREATE NONCLUSTERED INDEX [forum_id] ON [dbo].[phpbb_log]
(
   [forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics_track'  AND sc.name = N'dbo'  AND si.name = N'forum_id' AND so.type in (N'U'))
   DROP INDEX [forum_id] ON [dbo].[phpbb_topics_track] 
GO
CREATE NONCLUSTERED INDEX [forum_id] ON [dbo].[phpbb_topics_track]
(
   [forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_moderator_cache'  AND sc.name = N'dbo'  AND si.name = N'forum_id' AND so.type in (N'U'))
   DROP INDEX [forum_id] ON [dbo].[phpbb_moderator_cache] 
GO
CREATE NONCLUSTERED INDEX [forum_id] ON [dbo].[phpbb_moderator_cache]
(
   [forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_forums_watch'  AND sc.name = N'dbo'  AND si.name = N'forum_id' AND so.type in (N'U'))
   DROP INDEX [forum_id] ON [dbo].[phpbb_forums_watch] 
GO
CREATE NONCLUSTERED INDEX [forum_id] ON [dbo].[phpbb_forums_watch]
(
   [forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'forum_id' AND so.type in (N'U'))
   DROP INDEX [forum_id] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [forum_id] ON [dbo].[phpbb_posts]
(
   [forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND si.name = N'forum_id' AND so.type in (N'U'))
   DROP INDEX [forum_id] ON [dbo].[phpbb_topics] 
GO
CREATE NONCLUSTERED INDEX [forum_id] ON [dbo].[phpbb_topics]
(
   [forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND si.name = N'forum_id_type' AND so.type in (N'U'))
   DROP INDEX [forum_id_type] ON [dbo].[phpbb_topics] 
GO
CREATE NONCLUSTERED INDEX [forum_id_type] ON [dbo].[phpbb_topics]
(
   [forum_id] ASC,
   [topic_type] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_forums'  AND sc.name = N'dbo'  AND si.name = N'forum_lastpost_id' AND so.type in (N'U'))
   DROP INDEX [forum_lastpost_id] ON [dbo].[phpbb_forums] 
GO
CREATE NONCLUSTERED INDEX [forum_lastpost_id] ON [dbo].[phpbb_forums]
(
   [forum_last_post_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_groups'  AND sc.name = N'dbo'  AND si.name = N'group_id' AND so.type in (N'U'))
   DROP INDEX [group_id] ON [dbo].[phpbb_acl_groups] 
GO
CREATE NONCLUSTERED INDEX [group_id] ON [dbo].[phpbb_acl_groups]
(
   [group_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_user_group'  AND sc.name = N'dbo'  AND si.name = N'group_id' AND so.type in (N'U'))
   DROP INDEX [group_id] ON [dbo].[phpbb_user_group] 
GO
CREATE NONCLUSTERED INDEX [group_id] ON [dbo].[phpbb_user_group]
(
   [group_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_user_group'  AND sc.name = N'dbo'  AND si.name = N'group_leader' AND so.type in (N'U'))
   DROP INDEX [group_leader] ON [dbo].[phpbb_user_group] 
GO
CREATE NONCLUSTERED INDEX [group_leader] ON [dbo].[phpbb_user_group]
(
   [group_leader] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_groups'  AND sc.name = N'dbo'  AND si.name = N'group_legend_name' AND so.type in (N'U'))
   DROP INDEX [group_legend_name] ON [dbo].[phpbb_groups] 
GO
CREATE NONCLUSTERED INDEX [group_legend_name] ON [dbo].[phpbb_groups]
(
   [group_legend] ASC,
   [group_name] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_styles_imageset_data'  AND sc.name = N'dbo'  AND si.name = N'i_d' AND so.type in (N'U'))
   DROP INDEX [i_d] ON [dbo].[phpbb_styles_imageset_data] 
GO
CREATE NONCLUSTERED INDEX [i_d] ON [dbo].[phpbb_styles_imageset_data]
(
   [imageset_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_styles'  AND sc.name = N'dbo'  AND si.name = N'imageset_id' AND so.type in (N'U'))
   DROP INDEX [imageset_id] ON [dbo].[phpbb_styles] 
GO
CREATE NONCLUSTERED INDEX [imageset_id] ON [dbo].[phpbb_styles]
(
   [imageset_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_config'  AND sc.name = N'dbo'  AND si.name = N'is_dynamic' AND so.type in (N'U'))
   DROP INDEX [is_dynamic] ON [dbo].[phpbb_config] 
GO
CREATE NONCLUSTERED INDEX [is_dynamic] ON [dbo].[phpbb_config]
(
   [is_dynamic] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND si.name = N'is_orphan' AND so.type in (N'U'))
   DROP INDEX [is_orphan] ON [dbo].[phpbb_attachments] 
GO
CREATE NONCLUSTERED INDEX [is_orphan] ON [dbo].[phpbb_attachments]
(
   [is_orphan] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_lang'  AND sc.name = N'dbo'  AND si.name = N'lang_iso' AND so.type in (N'U'))
   DROP INDEX [lang_iso] ON [dbo].[phpbb_lang] 
GO
CREATE NONCLUSTERED INDEX [lang_iso] ON [dbo].[phpbb_lang]
(
   [lang_iso] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_captcha_questions'  AND sc.name = N'dbo'  AND si.name = N'lang_iso' AND so.type in (N'U'))
   DROP INDEX [lang_iso] ON [dbo].[phpbb_captcha_questions] 
GO
CREATE NONCLUSTERED INDEX [lang_iso] ON [dbo].[phpbb_captcha_questions]
(
   [lang_iso] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_sessions_keys'  AND sc.name = N'dbo'  AND si.name = N'last_login' AND so.type in (N'U'))
   DROP INDEX [last_login] ON [dbo].[phpbb_sessions_keys] 
GO
CREATE NONCLUSTERED INDEX [last_login] ON [dbo].[phpbb_sessions_keys]
(
   [last_login] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND si.name = N'last_post_time' AND so.type in (N'U'))
   DROP INDEX [last_post_time] ON [dbo].[phpbb_topics] 
GO
CREATE NONCLUSTERED INDEX [last_post_time] ON [dbo].[phpbb_topics]
(
   [topic_last_post_time] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_modules'  AND sc.name = N'dbo'  AND si.name = N'left_right_id' AND so.type in (N'U'))
   DROP INDEX [left_right_id] ON [dbo].[phpbb_modules] 
GO
CREATE NONCLUSTERED INDEX [left_right_id] ON [dbo].[phpbb_modules]
(
   [left_id] ASC,
   [right_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_forums'  AND sc.name = N'dbo'  AND si.name = N'left_right_id' AND so.type in (N'U'))
   DROP INDEX [left_right_id] ON [dbo].[phpbb_forums] 
GO
CREATE NONCLUSTERED INDEX [left_right_id] ON [dbo].[phpbb_forums]
(
   [left_id] ASC,
   [right_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND si.name = N'log_type' AND so.type in (N'U'))
   DROP INDEX [log_type] ON [dbo].[phpbb_log] 
GO
CREATE NONCLUSTERED INDEX [log_type] ON [dbo].[phpbb_log]
(
   [log_type] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_qa_confirm'  AND sc.name = N'dbo'  AND si.name = N'lookup' AND so.type in (N'U'))
   DROP INDEX [lookup] ON [dbo].[phpbb_qa_confirm] 
GO
CREATE NONCLUSTERED INDEX [lookup] ON [dbo].[phpbb_qa_confirm]
(
   [confirm_id] ASC,
   [session_id] ASC,
   [lang_iso] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs'  AND sc.name = N'dbo'  AND si.name = N'message_time' AND so.type in (N'U'))
   DROP INDEX [message_time] ON [dbo].[phpbb_privmsgs] 
GO
CREATE NONCLUSTERED INDEX [message_time] ON [dbo].[phpbb_privmsgs]
(
   [message_time] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_modules'  AND sc.name = N'dbo'  AND si.name = N'module_enabled' AND so.type in (N'U'))
   DROP INDEX [module_enabled] ON [dbo].[phpbb_modules] 
GO
CREATE NONCLUSTERED INDEX [module_enabled] ON [dbo].[phpbb_modules]
(
   [module_enabled] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs_to'  AND sc.name = N'dbo'  AND si.name = N'msg_id' AND so.type in (N'U'))
   DROP INDEX [msg_id] ON [dbo].[phpbb_privmsgs_to] 
GO
CREATE NONCLUSTERED INDEX [msg_id] ON [dbo].[phpbb_privmsgs_to]
(
   [msg_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics_watch'  AND sc.name = N'dbo'  AND si.name = N'notify_stat' AND so.type in (N'U'))
   DROP INDEX [notify_stat] ON [dbo].[phpbb_topics_watch] 
GO
CREATE NONCLUSTERED INDEX [notify_stat] ON [dbo].[phpbb_topics_watch]
(
   [notify_status] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_forums_watch'  AND sc.name = N'dbo'  AND si.name = N'notify_stat' AND so.type in (N'U'))
   DROP INDEX [notify_stat] ON [dbo].[phpbb_forums_watch] 
GO
CREATE NONCLUSTERED INDEX [notify_stat] ON [dbo].[phpbb_forums_watch]
(
   [notify_status] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'pid_post_time' AND so.type in (N'U'))
   DROP INDEX [pid_post_time] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [pid_post_time] ON [dbo].[phpbb_posts]
(
   [post_id] ASC,
   [topic_id] ASC,
   [poster_id] ASC,
   [post_time] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_reports'  AND sc.name = N'dbo'  AND si.name = N'pm_id' AND so.type in (N'U'))
   DROP INDEX [pm_id] ON [dbo].[phpbb_reports] 
GO
CREATE NONCLUSTERED INDEX [pm_id] ON [dbo].[phpbb_reports]
(
   [pm_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_poll_options'  AND sc.name = N'dbo'  AND si.name = N'poll_opt_id' AND so.type in (N'U'))
   DROP INDEX [poll_opt_id] ON [dbo].[phpbb_poll_options] 
GO
CREATE NONCLUSTERED INDEX [poll_opt_id] ON [dbo].[phpbb_poll_options]
(
   [poll_option_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'post_approved' AND so.type in (N'U'))
   DROP INDEX [post_approved] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [post_approved] ON [dbo].[phpbb_posts]
(
   [post_approved] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_search_wordmatch'  AND sc.name = N'dbo'  AND si.name = N'post_id' AND so.type in (N'U'))
   DROP INDEX [post_id] ON [dbo].[phpbb_search_wordmatch] 
GO
CREATE NONCLUSTERED INDEX [post_id] ON [dbo].[phpbb_search_wordmatch]
(
   [post_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_reports'  AND sc.name = N'dbo'  AND si.name = N'post_id' AND so.type in (N'U'))
   DROP INDEX [post_id] ON [dbo].[phpbb_reports] 
GO
CREATE NONCLUSTERED INDEX [post_id] ON [dbo].[phpbb_reports]
(
   [post_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND si.name = N'post_msg_id' AND so.type in (N'U'))
   DROP INDEX [post_msg_id] ON [dbo].[phpbb_attachments] 
GO
CREATE NONCLUSTERED INDEX [post_msg_id] ON [dbo].[phpbb_attachments]
(
   [post_msg_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'post_username' AND so.type in (N'U'))
   DROP INDEX [post_username] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [post_username] ON [dbo].[phpbb_posts]
(
   [post_username] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND si.name = N'poster_id' AND so.type in (N'U'))
   DROP INDEX [poster_id] ON [dbo].[phpbb_attachments] 
GO
CREATE NONCLUSTERED INDEX [poster_id] ON [dbo].[phpbb_attachments]
(
   [poster_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'poster_id' AND so.type in (N'U'))
   DROP INDEX [poster_id] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [poster_id] ON [dbo].[phpbb_posts]
(
   [poster_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'poster_ip' AND so.type in (N'U'))
   DROP INDEX [poster_ip] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [poster_ip] ON [dbo].[phpbb_posts]
(
   [poster_ip] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_captcha_answers'  AND sc.name = N'dbo'  AND si.name = N'question_id' AND so.type in (N'U'))
   DROP INDEX [question_id] ON [dbo].[phpbb_captcha_answers] 
GO
CREATE NONCLUSTERED INDEX [question_id] ON [dbo].[phpbb_captcha_answers]
(
   [question_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND si.name = N'reportee_id' AND so.type in (N'U'))
   DROP INDEX [reportee_id] ON [dbo].[phpbb_log] 
GO
CREATE NONCLUSTERED INDEX [reportee_id] ON [dbo].[phpbb_log]
(
   [reportee_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_roles'  AND sc.name = N'dbo'  AND si.name = N'role_order' AND so.type in (N'U'))
   DROP INDEX [role_order] ON [dbo].[phpbb_acl_roles] 
GO
CREATE NONCLUSTERED INDEX [role_order] ON [dbo].[phpbb_acl_roles]
(
   [role_order] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_roles'  AND sc.name = N'dbo'  AND si.name = N'role_type' AND so.type in (N'U'))
   DROP INDEX [role_type] ON [dbo].[phpbb_acl_roles] 
GO
CREATE NONCLUSTERED INDEX [role_type] ON [dbo].[phpbb_acl_roles]
(
   [role_type] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs'  AND sc.name = N'dbo'  AND si.name = N'root_level' AND so.type in (N'U'))
   DROP INDEX [root_level] ON [dbo].[phpbb_privmsgs] 
GO
CREATE NONCLUSTERED INDEX [root_level] ON [dbo].[phpbb_privmsgs]
(
   [root_level] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_drafts'  AND sc.name = N'dbo'  AND si.name = N'save_time' AND so.type in (N'U'))
   DROP INDEX [save_time] ON [dbo].[phpbb_drafts] 
GO
CREATE NONCLUSTERED INDEX [save_time] ON [dbo].[phpbb_drafts]
(
   [save_time] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_sessions'  AND sc.name = N'dbo'  AND si.name = N'session_fid' AND so.type in (N'U'))
   DROP INDEX [session_fid] ON [dbo].[phpbb_sessions] 
GO
CREATE NONCLUSTERED INDEX [session_fid] ON [dbo].[phpbb_sessions]
(
   [session_forum_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_qa_confirm'  AND sc.name = N'dbo'  AND si.name = N'session_id' AND so.type in (N'U'))
   DROP INDEX [session_id] ON [dbo].[phpbb_qa_confirm] 
GO
CREATE NONCLUSTERED INDEX [session_id] ON [dbo].[phpbb_qa_confirm]
(
   [session_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_sessions'  AND sc.name = N'dbo'  AND si.name = N'session_time' AND so.type in (N'U'))
   DROP INDEX [session_time] ON [dbo].[phpbb_sessions] 
GO
CREATE NONCLUSTERED INDEX [session_time] ON [dbo].[phpbb_sessions]
(
   [session_time] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_sessions'  AND sc.name = N'dbo'  AND si.name = N'session_user_id' AND so.type in (N'U'))
   DROP INDEX [session_user_id] ON [dbo].[phpbb_sessions] 
GO
CREATE NONCLUSTERED INDEX [session_user_id] ON [dbo].[phpbb_sessions]
(
   [session_user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_styles'  AND sc.name = N'dbo'  AND si.name = N'template_id' AND so.type in (N'U'))
   DROP INDEX [template_id] ON [dbo].[phpbb_styles] 
GO
CREATE NONCLUSTERED INDEX [template_id] ON [dbo].[phpbb_styles]
(
   [template_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_styles_template_data'  AND sc.name = N'dbo'  AND si.name = N'tfn' AND so.type in (N'U'))
   DROP INDEX [tfn] ON [dbo].[phpbb_styles_template_data] 
GO
CREATE NONCLUSTERED INDEX [tfn] ON [dbo].[phpbb_styles_template_data]
(
   [template_filename] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_styles'  AND sc.name = N'dbo'  AND si.name = N'theme_id' AND so.type in (N'U'))
   DROP INDEX [theme_id] ON [dbo].[phpbb_styles] 
GO
CREATE NONCLUSTERED INDEX [theme_id] ON [dbo].[phpbb_styles]
(
   [theme_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_styles_template_data'  AND sc.name = N'dbo'  AND si.name = N'tid' AND so.type in (N'U'))
   DROP INDEX [tid] ON [dbo].[phpbb_styles_template_data] 
GO
CREATE NONCLUSTERED INDEX [tid] ON [dbo].[phpbb_styles_template_data]
(
   [template_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'tid_post_time' AND so.type in (N'U'))
   DROP INDEX [tid_post_time] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [tid_post_time] ON [dbo].[phpbb_posts]
(
   [topic_id] ASC,
   [post_time] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics'  AND sc.name = N'dbo'  AND si.name = N'topic_approved' AND so.type in (N'U'))
   DROP INDEX [topic_approved] ON [dbo].[phpbb_topics] 
GO
CREATE NONCLUSTERED INDEX [topic_approved] ON [dbo].[phpbb_topics]
(
   [topic_approved] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics_track'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_topics_track] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_topics_track]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_poll_votes'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_poll_votes] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_poll_votes]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_log] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_log]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_attachments'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_attachments] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_attachments]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_poll_options'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_poll_options] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_poll_options]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics_watch'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_topics_watch] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_topics_watch]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_posts'  AND sc.name = N'dbo'  AND si.name = N'topic_id' AND so.type in (N'U'))
   DROP INDEX [topic_id] ON [dbo].[phpbb_posts] 
GO
CREATE NONCLUSTERED INDEX [topic_id] ON [dbo].[phpbb_posts]
(
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_users'  AND sc.name = N'dbo'  AND si.name = N'user_birthday' AND so.type in (N'U'))
   DROP INDEX [user_birthday] ON [dbo].[phpbb_users] 
GO
CREATE NONCLUSTERED INDEX [user_birthday] ON [dbo].[phpbb_users]
(
   [user_birthday] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_users'  AND sc.name = N'dbo'  AND si.name = N'user_email_hash' AND so.type in (N'U'))
   DROP INDEX [user_email_hash] ON [dbo].[phpbb_users] 
GO
CREATE NONCLUSTERED INDEX [user_email_hash] ON [dbo].[phpbb_users]
(
   [user_email_hash] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs_rules'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_privmsgs_rules] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_privmsgs_rules]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs_folder'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_privmsgs_folder] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_privmsgs_folder]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_user_group'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_user_group] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_user_group]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_topics_watch'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_topics_watch] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_topics_watch]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_log'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_log] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_log]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_user_topic_post_number'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_user_topic_post_number] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_user_topic_post_number]
(
   [user_id] ASC,
   [topic_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_acl_users'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_acl_users] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_acl_users]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_forums_watch'  AND sc.name = N'dbo'  AND si.name = N'user_id' AND so.type in (N'U'))
   DROP INDEX [user_id] ON [dbo].[phpbb_forums_watch] 
GO
CREATE NONCLUSTERED INDEX [user_id] ON [dbo].[phpbb_forums_watch]
(
   [user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_users'  AND sc.name = N'dbo'  AND si.name = N'user_type' AND so.type in (N'U'))
   DROP INDEX [user_type] ON [dbo].[phpbb_users] 
GO
CREATE NONCLUSTERED INDEX [user_type] ON [dbo].[phpbb_users]
(
   [user_type] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_privmsgs_to'  AND sc.name = N'dbo'  AND si.name = N'usr_flder_id' AND so.type in (N'U'))
   DROP INDEX [usr_flder_id] ON [dbo].[phpbb_privmsgs_to] 
GO
CREATE NONCLUSTERED INDEX [usr_flder_id] ON [dbo].[phpbb_privmsgs_to]
(
   [user_id] ASC,
   [folder_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_poll_votes'  AND sc.name = N'dbo'  AND si.name = N'vote_user_id' AND so.type in (N'U'))
   DROP INDEX [vote_user_id] ON [dbo].[phpbb_poll_votes] 
GO
CREATE NONCLUSTERED INDEX [vote_user_id] ON [dbo].[phpbb_poll_votes]
(
   [vote_user_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_poll_votes'  AND sc.name = N'dbo'  AND si.name = N'vote_user_ip' AND so.type in (N'U'))
   DROP INDEX [vote_user_ip] ON [dbo].[phpbb_poll_votes] 
GO
CREATE NONCLUSTERED INDEX [vote_user_ip] ON [dbo].[phpbb_poll_votes]
(
   [vote_user_ip] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_search_wordmatch'  AND sc.name = N'dbo'  AND si.name = N'word_id' AND so.type in (N'U'))
   DROP INDEX [word_id] ON [dbo].[phpbb_search_wordmatch] 
GO
CREATE NONCLUSTERED INDEX [word_id] ON [dbo].[phpbb_search_wordmatch]
(
   [word_id] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
IF EXISTS (
       SELECT * FROM sys.objects  so JOIN sys.indexes si
       ON so.object_id = si.object_id
       JOIN sys.schemas sc
       ON so.schema_id = sc.schema_id
       WHERE so.name = N'phpbb_search_wordlist'  AND sc.name = N'dbo'  AND si.name = N'wrd_cnt' AND so.type in (N'U'))
   DROP INDEX [wrd_cnt] ON [dbo].[phpbb_search_wordlist] 
GO
CREATE NONCLUSTERED INDEX [wrd_cnt] ON [dbo].[phpbb_search_wordlist]
(
   [word_count] ASC
)
WITH (DROP_EXISTING = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF)
GO
GO
ALTER TABLE  [dbo].[phpbb_acl_groups]
 ADD DEFAULT 0 FOR [group_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_groups]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_groups]
 ADD DEFAULT 0 FOR [auth_option_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_groups]
 ADD DEFAULT 0 FOR [auth_role_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_groups]
 ADD DEFAULT 0 FOR [auth_setting]
GO

ALTER TABLE  [dbo].[phpbb_acl_options]
 ADD DEFAULT N'' FOR [auth_option]
GO

ALTER TABLE  [dbo].[phpbb_acl_options]
 ADD DEFAULT 0 FOR [is_global]
GO

ALTER TABLE  [dbo].[phpbb_acl_options]
 ADD DEFAULT 0 FOR [is_local]
GO

ALTER TABLE  [dbo].[phpbb_acl_options]
 ADD DEFAULT 0 FOR [founder_only]
GO

ALTER TABLE  [dbo].[phpbb_acl_roles]
 ADD DEFAULT N'' FOR [role_name]
GO

ALTER TABLE  [dbo].[phpbb_acl_roles]
 ADD DEFAULT N'' FOR [role_type]
GO

ALTER TABLE  [dbo].[phpbb_acl_roles]
 ADD DEFAULT 0 FOR [role_order]
GO

ALTER TABLE  [dbo].[phpbb_acl_roles_data]
 ADD DEFAULT 0 FOR [role_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_roles_data]
 ADD DEFAULT 0 FOR [auth_option_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_roles_data]
 ADD DEFAULT 0 FOR [auth_setting]
GO

ALTER TABLE  [dbo].[phpbb_acl_users]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_users]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_users]
 ADD DEFAULT 0 FOR [auth_option_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_users]
 ADD DEFAULT 0 FOR [auth_role_id]
GO

ALTER TABLE  [dbo].[phpbb_acl_users]
 ADD DEFAULT 0 FOR [auth_setting]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [post_msg_id]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [in_message]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [poster_id]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 1 FOR [is_orphan]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT N'' FOR [physical_filename]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT N'' FOR [real_filename]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [download_count]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT N'' FOR [extension]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT N'' FOR [mimetype]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [filesize]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [filetime]
GO

ALTER TABLE  [dbo].[phpbb_attachments]
 ADD DEFAULT 0 FOR [thumbnail]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT 0 FOR [ban_userid]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT N'' FOR [ban_ip]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT N'' FOR [ban_email]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT 0 FOR [ban_start]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT 0 FOR [ban_end]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT 0 FOR [ban_exclude]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT N'' FOR [ban_reason]
GO

ALTER TABLE  [dbo].[phpbb_banlist]
 ADD DEFAULT N'' FOR [ban_give_reason]
GO

ALTER TABLE  [dbo].[phpbb_bbcodes]
 ADD DEFAULT N'' FOR [bbcode_tag]
GO

ALTER TABLE  [dbo].[phpbb_bbcodes]
 ADD DEFAULT N'' FOR [bbcode_helpline]
GO

ALTER TABLE  [dbo].[phpbb_bbcodes]
 ADD DEFAULT 0 FOR [display_on_posting]
GO

ALTER TABLE  [dbo].[phpbb_bookmarks]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_bookmarks]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_bots]
 ADD DEFAULT 1 FOR [bot_active]
GO

ALTER TABLE  [dbo].[phpbb_bots]
 ADD DEFAULT N'' FOR [bot_name]
GO

ALTER TABLE  [dbo].[phpbb_bots]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_bots]
 ADD DEFAULT N'' FOR [bot_agent]
GO

ALTER TABLE  [dbo].[phpbb_bots]
 ADD DEFAULT N'' FOR [bot_ip]
GO

ALTER TABLE  [dbo].[phpbb_captcha_answers]
 ADD DEFAULT 0 FOR [question_id]
GO

ALTER TABLE  [dbo].[phpbb_captcha_answers]
 ADD DEFAULT N'' FOR [answer_text]
GO

ALTER TABLE  [dbo].[phpbb_captcha_questions]
 ADD DEFAULT 0 FOR [strict]
GO

ALTER TABLE  [dbo].[phpbb_captcha_questions]
 ADD DEFAULT 0 FOR [lang_id]
GO

ALTER TABLE  [dbo].[phpbb_captcha_questions]
 ADD DEFAULT N'' FOR [lang_iso]
GO

ALTER TABLE  [dbo].[phpbb_config]
 ADD DEFAULT N'' FOR [config_name]
GO

ALTER TABLE  [dbo].[phpbb_config]
 ADD DEFAULT N'' FOR [config_value]
GO

ALTER TABLE  [dbo].[phpbb_config]
 ADD DEFAULT 0 FOR [is_dynamic]
GO

ALTER TABLE  [dbo].[phpbb_confirm]
 ADD DEFAULT N'' FOR [confirm_id]
GO

ALTER TABLE  [dbo].[phpbb_confirm]
 ADD DEFAULT N'' FOR [session_id]
GO

ALTER TABLE  [dbo].[phpbb_confirm]
 ADD DEFAULT 0 FOR [confirm_type]
GO

ALTER TABLE  [dbo].[phpbb_confirm]
 ADD DEFAULT N'' FOR [code]
GO

ALTER TABLE  [dbo].[phpbb_confirm]
 ADD DEFAULT 0 FOR [seed]
GO

ALTER TABLE  [dbo].[phpbb_confirm]
 ADD DEFAULT 0 FOR [attempts]
GO

ALTER TABLE  [dbo].[phpbb_disallow]
 ADD DEFAULT N'' FOR [disallow_username]
GO

ALTER TABLE  [dbo].[phpbb_drafts]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_drafts]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_drafts]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_drafts]
 ADD DEFAULT 0 FOR [save_time]
GO

ALTER TABLE  [dbo].[phpbb_drafts]
 ADD DEFAULT N'' FOR [draft_subject]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT N'' FOR [group_name]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT 0 FOR [cat_id]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT 0 FOR [allow_group]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT 1 FOR [download_mode]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT N'' FOR [upload_icon]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT 0 FOR [max_filesize]
GO

ALTER TABLE  [dbo].[phpbb_extension_groups]
 ADD DEFAULT 0 FOR [allow_in_pm]
GO

ALTER TABLE  [dbo].[phpbb_extensions]
 ADD DEFAULT 0 FOR [group_id]
GO

ALTER TABLE  [dbo].[phpbb_extensions]
 ADD DEFAULT N'' FOR [extension]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [parent_id]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [left_id]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [right_id]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_name]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_desc_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 7 FOR [forum_desc_options]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_desc_uid]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_link]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_password]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_style]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_image]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_rules_link]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_rules_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 7 FOR [forum_rules_options]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_rules_uid]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_topics_per_page]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_type]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_status]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_posts]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_topics]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_topics_real]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_last_post_id]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_last_poster_id]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_last_post_subject]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_last_post_time]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_last_poster_name]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT N'' FOR [forum_last_poster_colour]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 32 FOR [forum_flags]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_options]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 1 FOR [display_subforum_list]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 1 FOR [display_on_index]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 1 FOR [enable_indexing]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 1 FOR [enable_icons]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [enable_prune]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [prune_next]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [prune_days]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [prune_viewed]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [prune_freq]
GO

ALTER TABLE  [dbo].[phpbb_forums]
 ADD DEFAULT 0 FOR [forum_edit_time]
GO

ALTER TABLE  [dbo].[phpbb_forums_access]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_access]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_access]
 ADD DEFAULT N'' FOR [session_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_track]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_track]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_track]
 ADD DEFAULT 0 FOR [mark_time]
GO

ALTER TABLE  [dbo].[phpbb_forums_watch]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_watch]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_forums_watch]
 ADD DEFAULT 0 FOR [notify_status]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 1 FOR [group_type]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_founder_manage]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_skip_auth]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT N'' FOR [group_name]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT N'' FOR [group_desc_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 7 FOR [group_desc_options]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT N'' FOR [group_desc_uid]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_display]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT N'' FOR [group_avatar]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_avatar_type]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_avatar_width]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_avatar_height]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_rank]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT N'' FOR [group_colour]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_sig_chars]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_receive_pm]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_message_limit]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_max_recipients]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 1 FOR [group_legend]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 0 FOR [group_user_upload_size]
GO

ALTER TABLE  [dbo].[phpbb_groups]
 ADD DEFAULT 60 FOR [group_edit_time]
GO

ALTER TABLE  [dbo].[phpbb_icons]
 ADD DEFAULT N'' FOR [icons_url]
GO

ALTER TABLE  [dbo].[phpbb_icons]
 ADD DEFAULT 0 FOR [icons_width]
GO

ALTER TABLE  [dbo].[phpbb_icons]
 ADD DEFAULT 0 FOR [icons_height]
GO

ALTER TABLE  [dbo].[phpbb_icons]
 ADD DEFAULT 0 FOR [icons_order]
GO

ALTER TABLE  [dbo].[phpbb_icons]
 ADD DEFAULT 1 FOR [display_on_posting]
GO

ALTER TABLE  [dbo].[phpbb_lang]
 ADD DEFAULT N'' FOR [lang_iso]
GO

ALTER TABLE  [dbo].[phpbb_lang]
 ADD DEFAULT N'' FOR [lang_dir]
GO

ALTER TABLE  [dbo].[phpbb_lang]
 ADD DEFAULT N'' FOR [lang_english_name]
GO

ALTER TABLE  [dbo].[phpbb_lang]
 ADD DEFAULT N'' FOR [lang_local_name]
GO

ALTER TABLE  [dbo].[phpbb_lang]
 ADD DEFAULT N'' FOR [lang_author]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT 0 FOR [log_type]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT 0 FOR [reportee_id]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT N'' FOR [log_ip]
GO

ALTER TABLE  [dbo].[phpbb_log]
 ADD DEFAULT 0 FOR [log_time]
GO

ALTER TABLE  [dbo].[phpbb_moderator_cache]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_moderator_cache]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_moderator_cache]
 ADD DEFAULT N'' FOR [username]
GO

ALTER TABLE  [dbo].[phpbb_moderator_cache]
 ADD DEFAULT 0 FOR [group_id]
GO

ALTER TABLE  [dbo].[phpbb_moderator_cache]
 ADD DEFAULT N'' FOR [group_name]
GO

ALTER TABLE  [dbo].[phpbb_moderator_cache]
 ADD DEFAULT 1 FOR [display_on_index]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT 1 FOR [module_enabled]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT 1 FOR [module_display]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT N'' FOR [module_basename]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT N'' FOR [module_class]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT 0 FOR [parent_id]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT 0 FOR [left_id]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT 0 FOR [right_id]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT N'' FOR [module_langname]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT N'' FOR [module_mode]
GO

ALTER TABLE  [dbo].[phpbb_modules]
 ADD DEFAULT N'' FOR [module_auth]
GO

ALTER TABLE  [dbo].[phpbb_poll_options]
 ADD DEFAULT 0 FOR [poll_option_id]
GO

ALTER TABLE  [dbo].[phpbb_poll_options]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_poll_options]
 ADD DEFAULT 0 FOR [poll_option_total]
GO

ALTER TABLE  [dbo].[phpbb_poll_votes]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_poll_votes]
 ADD DEFAULT 0 FOR [poll_option_id]
GO

ALTER TABLE  [dbo].[phpbb_poll_votes]
 ADD DEFAULT 0 FOR [vote_user_id]
GO

ALTER TABLE  [dbo].[phpbb_poll_votes]
 ADD DEFAULT N'' FOR [vote_user_ip]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [poster_id]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [icon_id]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [poster_ip]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_time]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 1 FOR [post_approved]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_reported]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 1 FOR [enable_bbcode]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 1 FOR [enable_smilies]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 1 FOR [enable_magic_url]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 1 FOR [enable_sig]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [post_username]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [post_subject]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [post_checksum]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_attachment]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [bbcode_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [bbcode_uid]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 1 FOR [post_postcount]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_edit_time]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT N'' FOR [post_edit_reason]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_edit_user]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_edit_count]
GO

ALTER TABLE  [dbo].[phpbb_posts]
 ADD DEFAULT 0 FOR [post_edit_locked]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [root_level]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [author_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [icon_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT N'' FOR [author_ip]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [message_time]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 1 FOR [enable_bbcode]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 1 FOR [enable_smilies]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 1 FOR [enable_magic_url]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 1 FOR [enable_sig]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT N'' FOR [message_subject]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT N'' FOR [message_edit_reason]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [message_edit_user]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [message_attachment]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT N'' FOR [bbcode_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT N'' FOR [bbcode_uid]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [message_edit_time]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [message_edit_count]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs]
 ADD DEFAULT 0 FOR [message_reported]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_folder]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_folder]
 ADD DEFAULT N'' FOR [folder_name]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_folder]
 ADD DEFAULT 0 FOR [pm_count]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [rule_check]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [rule_connection]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT N'' FOR [rule_string]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [rule_user_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [rule_group_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [rule_action]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_rules]
 ADD DEFAULT 0 FOR [rule_folder_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [msg_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [author_id]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [pm_deleted]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 1 FOR [pm_new]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 1 FOR [pm_unread]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [pm_replied]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [pm_marked]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [pm_forwarded]
GO

ALTER TABLE  [dbo].[phpbb_privmsgs_to]
 ADD DEFAULT 0 FOR [folder_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_name]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_type]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_ident]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_length]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_minlen]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_maxlen]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_novalue]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_default_value]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT N'' FOR [field_validation]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_required]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_show_on_reg]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_show_on_vt]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_show_profile]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_hide]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_no_view]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_active]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields]
 ADD DEFAULT 0 FOR [field_order]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields_data]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields_lang]
 ADD DEFAULT 0 FOR [field_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields_lang]
 ADD DEFAULT 0 FOR [lang_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields_lang]
 ADD DEFAULT 0 FOR [option_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields_lang]
 ADD DEFAULT 0 FOR [field_type]
GO

ALTER TABLE  [dbo].[phpbb_profile_fields_lang]
 ADD DEFAULT N'' FOR [lang_value]
GO

ALTER TABLE  [dbo].[phpbb_profile_lang]
 ADD DEFAULT 0 FOR [field_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_lang]
 ADD DEFAULT 0 FOR [lang_id]
GO

ALTER TABLE  [dbo].[phpbb_profile_lang]
 ADD DEFAULT N'' FOR [lang_name]
GO

ALTER TABLE  [dbo].[phpbb_profile_lang]
 ADD DEFAULT N'' FOR [lang_default_value]
GO

ALTER TABLE  [dbo].[phpbb_qa_confirm]
 ADD DEFAULT N'' FOR [session_id]
GO

ALTER TABLE  [dbo].[phpbb_qa_confirm]
 ADD DEFAULT N'' FOR [confirm_id]
GO

ALTER TABLE  [dbo].[phpbb_qa_confirm]
 ADD DEFAULT N'' FOR [lang_iso]
GO

ALTER TABLE  [dbo].[phpbb_qa_confirm]
 ADD DEFAULT 0 FOR [question_id]
GO

ALTER TABLE  [dbo].[phpbb_qa_confirm]
 ADD DEFAULT 0 FOR [attempts]
GO

ALTER TABLE  [dbo].[phpbb_qa_confirm]
 ADD DEFAULT 0 FOR [confirm_type]
GO

ALTER TABLE  [dbo].[phpbb_ranks]
 ADD DEFAULT N'' FOR [rank_title]
GO

ALTER TABLE  [dbo].[phpbb_ranks]
 ADD DEFAULT 0 FOR [rank_min]
GO

ALTER TABLE  [dbo].[phpbb_ranks]
 ADD DEFAULT 0 FOR [rank_special]
GO

ALTER TABLE  [dbo].[phpbb_ranks]
 ADD DEFAULT N'' FOR [rank_image]
GO

ALTER TABLE  [dbo].[phpbb_recycle_bin]
 ADD DEFAULT NULL FOR [content]
GO

ALTER TABLE  [dbo].[phpbb_recycle_bin]
 ADD DEFAULT 0 FOR [delete_time]
GO

ALTER TABLE  [dbo].[phpbb_recycle_bin]
 ADD DEFAULT 0 FOR [delete_user]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [reason_id]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [post_id]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [pm_id]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [user_notify]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [report_closed]
GO

ALTER TABLE  [dbo].[phpbb_reports]
 ADD DEFAULT 0 FOR [report_time]
GO

ALTER TABLE  [dbo].[phpbb_reports_reasons]
 ADD DEFAULT N'' FOR [reason_title]
GO

ALTER TABLE  [dbo].[phpbb_reports_reasons]
 ADD DEFAULT 0 FOR [reason_order]
GO

ALTER TABLE  [dbo].[phpbb_search_results]
 ADD DEFAULT N'' FOR [search_key]
GO

ALTER TABLE  [dbo].[phpbb_search_results]
 ADD DEFAULT 0 FOR [search_time]
GO

ALTER TABLE  [dbo].[phpbb_search_wordlist]
 ADD DEFAULT N'' FOR [word_text]
GO

ALTER TABLE  [dbo].[phpbb_search_wordlist]
 ADD DEFAULT 0 FOR [word_common]
GO

ALTER TABLE  [dbo].[phpbb_search_wordlist]
 ADD DEFAULT 0 FOR [word_count]
GO

ALTER TABLE  [dbo].[phpbb_search_wordmatch]
 ADD DEFAULT 0 FOR [post_id]
GO

ALTER TABLE  [dbo].[phpbb_search_wordmatch]
 ADD DEFAULT 0 FOR [word_id]
GO

ALTER TABLE  [dbo].[phpbb_search_wordmatch]
 ADD DEFAULT 0 FOR [title_match]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT N'' FOR [session_id]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_user_id]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_forum_id]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_last_visit]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_start]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_time]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT N'' FOR [session_ip]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT N'' FOR [session_browser]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT N'' FOR [session_forwarded_for]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT N'' FOR [session_page]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 1 FOR [session_viewonline]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_autologin]
GO

ALTER TABLE  [dbo].[phpbb_sessions]
 ADD DEFAULT 0 FOR [session_admin]
GO

ALTER TABLE  [dbo].[phpbb_sessions_keys]
 ADD DEFAULT N'' FOR [key_id]
GO

ALTER TABLE  [dbo].[phpbb_sessions_keys]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_sessions_keys]
 ADD DEFAULT N'' FOR [last_ip]
GO

ALTER TABLE  [dbo].[phpbb_sessions_keys]
 ADD DEFAULT 0 FOR [last_login]
GO

ALTER TABLE  [dbo].[phpbb_shortcuts]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_shortcuts]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_sitelist]
 ADD DEFAULT N'' FOR [site_ip]
GO

ALTER TABLE  [dbo].[phpbb_sitelist]
 ADD DEFAULT N'' FOR [site_hostname]
GO

ALTER TABLE  [dbo].[phpbb_sitelist]
 ADD DEFAULT 0 FOR [ip_exclude]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT N'' FOR [code]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT N'' FOR [emotion]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT N'' FOR [smiley_url]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT 0 FOR [smiley_width]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT 0 FOR [smiley_height]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT 0 FOR [smiley_order]
GO

ALTER TABLE  [dbo].[phpbb_smilies]
 ADD DEFAULT 1 FOR [display_on_posting]
GO

ALTER TABLE  [dbo].[phpbb_styles]
 ADD DEFAULT N'' FOR [style_name]
GO

ALTER TABLE  [dbo].[phpbb_styles]
 ADD DEFAULT N'' FOR [style_copyright]
GO

ALTER TABLE  [dbo].[phpbb_styles]
 ADD DEFAULT 1 FOR [style_active]
GO

ALTER TABLE  [dbo].[phpbb_styles]
 ADD DEFAULT 0 FOR [template_id]
GO

ALTER TABLE  [dbo].[phpbb_styles]
 ADD DEFAULT 0 FOR [theme_id]
GO

ALTER TABLE  [dbo].[phpbb_styles]
 ADD DEFAULT 0 FOR [imageset_id]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset]
 ADD DEFAULT N'' FOR [imageset_name]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset]
 ADD DEFAULT N'' FOR [imageset_copyright]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset]
 ADD DEFAULT N'' FOR [imageset_path]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset_data]
 ADD DEFAULT N'' FOR [image_name]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset_data]
 ADD DEFAULT N'' FOR [image_filename]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset_data]
 ADD DEFAULT N'' FOR [image_lang]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset_data]
 ADD DEFAULT 0 FOR [image_height]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset_data]
 ADD DEFAULT 0 FOR [image_width]
GO

ALTER TABLE  [dbo].[phpbb_styles_imageset_data]
 ADD DEFAULT 0 FOR [imageset_id]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT N'' FOR [template_name]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT N'' FOR [template_copyright]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT N'' FOR [template_path]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT N'kNg=' FOR [bbcode_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT 0 FOR [template_storedb]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT 0 FOR [template_inherits_id]
GO

ALTER TABLE  [dbo].[phpbb_styles_template]
 ADD DEFAULT N'' FOR [template_inherit_path]
GO

ALTER TABLE  [dbo].[phpbb_styles_template_data]
 ADD DEFAULT 0 FOR [template_id]
GO

ALTER TABLE  [dbo].[phpbb_styles_template_data]
 ADD DEFAULT N'' FOR [template_filename]
GO

ALTER TABLE  [dbo].[phpbb_styles_template_data]
 ADD DEFAULT 0 FOR [template_mtime]
GO

ALTER TABLE  [dbo].[phpbb_styles_theme]
 ADD DEFAULT N'' FOR [theme_name]
GO

ALTER TABLE  [dbo].[phpbb_styles_theme]
 ADD DEFAULT N'' FOR [theme_copyright]
GO

ALTER TABLE  [dbo].[phpbb_styles_theme]
 ADD DEFAULT N'' FOR [theme_path]
GO

ALTER TABLE  [dbo].[phpbb_styles_theme]
 ADD DEFAULT 0 FOR [theme_storedb]
GO

ALTER TABLE  [dbo].[phpbb_styles_theme]
 ADD DEFAULT 0 FOR [theme_mtime]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [icon_id]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_attachment]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 1 FOR [topic_approved]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_reported]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [topic_title]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_poster]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_time]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_time_limit]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_views]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_replies]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_replies_real]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_status]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_type]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_first_post_id]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [topic_first_poster_name]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [topic_first_poster_colour]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_last_post_id]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_last_poster_id]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [topic_last_poster_name]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [topic_last_poster_colour]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [topic_last_post_subject]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_last_post_time]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_last_view_time]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_moved_id]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_bumped]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [topic_bumper]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT N'' FOR [poll_title]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [poll_start]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [poll_length]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 1 FOR [poll_max_options]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [poll_last_vote]
GO

ALTER TABLE  [dbo].[phpbb_topics]
 ADD DEFAULT 0 FOR [poll_vote_change]
GO

ALTER TABLE  [dbo].[phpbb_topics_posted]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_posted]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_posted]
 ADD DEFAULT 0 FOR [topic_posted]
GO

ALTER TABLE  [dbo].[phpbb_topics_track]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_track]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_track]
 ADD DEFAULT 0 FOR [forum_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_track]
 ADD DEFAULT 0 FOR [mark_time]
GO

ALTER TABLE  [dbo].[phpbb_topics_watch]
 ADD DEFAULT 0 FOR [topic_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_watch]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_topics_watch]
 ADD DEFAULT 0 FOR [notify_status]
GO

ALTER TABLE  [dbo].[phpbb_user_group]
 ADD DEFAULT 0 FOR [group_id]
GO

ALTER TABLE  [dbo].[phpbb_user_group]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_user_group]
 ADD DEFAULT 0 FOR [group_leader]
GO

ALTER TABLE  [dbo].[phpbb_user_group]
 ADD DEFAULT 1 FOR [user_pending]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_type]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 3 FOR [group_id]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_perm_from]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_ip]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_regdate]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [username]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [username_clean]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_password]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_passchg]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_pass_convert]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_email]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_email_hash]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_birthday]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_lastvisit]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_lastmark]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_lastpost_time]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_lastpage]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_last_confirm_key]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_last_search]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_warnings]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_last_warning]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_login_attempts]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_inactive_reason]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_inactive_time]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_posts]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_lang]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0.00 FOR [user_timezone]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_dst]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'dddd, dd.MM.yyyy, HH:mm' FOR [user_dateformat]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_style]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_rank]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_colour]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_new_privmsg]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_unread_privmsg]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_last_privmsg]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_message_rules]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT -3 FOR [user_full_folder]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_emailtime]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_topic_show_days]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N't' FOR [user_topic_sortby_type]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'd' FOR [user_topic_sortby_dir]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_post_show_days]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N't' FOR [user_post_sortby_type]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'a' FOR [user_post_sortby_dir]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_notify]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 1 FOR [user_notify_pm]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_notify_type]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 1 FOR [user_allow_pm]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 1 FOR [user_allow_viewonline]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_allow_viewemail]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 1 FOR [user_allow_massemail]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 230271 FOR [user_options]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_avatar]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_avatar_type]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_avatar_width]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_avatar_height]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_sig_bbcode_uid]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_sig_bbcode_bitfield]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_from]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_icq]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_aim]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_yim]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_msnm]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_jabber]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_website]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_actkey]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_newpasswd]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT N'' FOR [user_form_salt]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 1 FOR [user_new]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_reminded]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_reminded_time]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 60 FOR [user_edit_time]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 1 FOR [jump_to_unread]
GO

ALTER TABLE  [dbo].[phpbb_users]
 ADD DEFAULT 0 FOR [user_should_sign_in]
GO

ALTER TABLE  [dbo].[phpbb_warnings]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_warnings]
 ADD DEFAULT 0 FOR [post_id]
GO

ALTER TABLE  [dbo].[phpbb_warnings]
 ADD DEFAULT 0 FOR [log_id]
GO

ALTER TABLE  [dbo].[phpbb_warnings]
 ADD DEFAULT 0 FOR [warning_time]
GO

ALTER TABLE  [dbo].[phpbb_words]
 ADD DEFAULT N'' FOR [word]
GO

ALTER TABLE  [dbo].[phpbb_words]
 ADD DEFAULT N'' FOR [replacement]
GO

ALTER TABLE  [dbo].[phpbb_zebra]
 ADD DEFAULT 0 FOR [user_id]
GO

ALTER TABLE  [dbo].[phpbb_zebra]
 ADD DEFAULT 0 FOR [zebra_id]
GO

ALTER TABLE  [dbo].[phpbb_zebra]
 ADD DEFAULT 0 FOR [friend]
GO

ALTER TABLE  [dbo].[phpbb_zebra]
 ADD DEFAULT 0 FOR [foe]
GO

