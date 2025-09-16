ALTER TABLE phpbb_acl_groups 
ADD PRIMARY KEY (group_id,forum_id,auth_option_id,auth_role_id);

ALTER TABLE phpbb_acl_users 
ADD PRIMARY KEY (user_id,forum_id,auth_option_id,auth_role_id);

ALTER TABLE phpbb_bbcodes 
MODIFY COLUMN bbcode_id tinyint(3) NOT NULL AUTO_INCREMENT;

DROP TABLE IF EXISTS phpbb_captcha_answers;

DROP TABLE IF EXISTS phpbb_captcha_questions;

ALTER TABLE phpbb_confirm 
MODIFY COLUMN confirm_id varchar(32) COLLATE utf8_bin DEFAULT '' NOT NULL, 
MODIFY COLUMN session_id varchar(32) COLLATE utf8_bin DEFAULT '' NOT NULL;

ALTER TABLE phpbb_forums_watch 
ADD PRIMARY KEY (forum_id,user_id);

ALTER TABLE phpbb_groups
ADD COLUMN group_user_upload_size int(8) unsigned DEFAULT '0' NOT NULL,
ADD COLUMN group_edit_time int(4) unsigned NOT NULL DEFAULT '6';

ALTER TABLE phpbb_moderator_cache 
ADD PRIMARY KEY (forum_id,user_id,group_id);

ALTER TABLE phpbb_posts 
MODIFY COLUMN post_text mediumtext COLLATE utf8_unicode_ci NOT NULL,
ADD KEY pid_post_time (post_id,topic_id,poster_id,post_time);

ALTER TABLE phpbb_posts 
ADD FULLTEXT KEY post_subject (post_subject);

ALTER TABLE phpbb_posts 
ADD FULLTEXT KEY post_text (post_text);

ALTER TABLE phpbb_posts 
ADD FULLTEXT KEY post_content (post_subject,post_text);

ALTER TABLE phpbb_privmsgs_to
ADD COLUMN id bigint(20) NOT NULL AUTO_INCREMENT,
ADD PRIMARY KEY (id);

DROP TABLE IF EXISTS phpbb_qa_confirm;

ALTER TABLE phpbb_topics_watch 
ADD PRIMARY KEY (topic_id,user_id);

ALTER TABLE phpbb_users
MODIFY COLUMN user_password varchar(255) COLLATE utf8_bin DEFAULT '' NOT NULL,
MODIFY COLUMN user_dateformat varchar(30) COLLATE utf8_bin NOT NULL DEFAULT 'dddd, dd.MM.yyyy, HH:mm',
MODIFY COLUMN user_newpasswd varchar(255) COLLATE utf8_bin DEFAULT '' NOT NULL,
ADD COLUMN user_edit_time int(4) unsigned NOT NULL DEFAULT '6',
ADD COLUMN jump_to_unread tinyint(1) DEFAULT '1';

CREATE TABLE `phpbb_user_topic_post_number` (
  `user_id` int(11) NOT NULL,
  `topic_id` int(11) NOT NULL,
  `post_no` int(11) NOT NULL,
  PRIMARY KEY (`user_id`,`topic_id`),
  KEY `user_id` (`user_id`,`topic_id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1;

CREATE TABLE `phpbb_recycle_bin` (
  `type` int(11) NOT NULL,
  `id` int(11) NOT NULL,
  `content` longblob,
  `delete_time` int(11) unsigned NOT NULL DEFAULT '0',
  `delete_user` int(8) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`type`,`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

CREATE TABLE `phpbb_shortcuts` (
  `topic_id` MEDIUMINT(8) UNSIGNED NOT NULL DEFAULT '0',
  `forum_id` MEDIUMINT(8) UNSIGNED NOT NULL DEFAULT '0',
  PRIMARY KEY (`topic_id`, `forum_id`)
) ENGINE = MyISAM DEFAULT CHARACTER SET = utf8 COLLATE = utf8_bin;

ALTER TABLE phpbb_attachments 
ADD FULLTEXT KEY attach_comment (attach_comment);

ALTER TABLE phpbb_attachments
ADD FULLTEXT KEY real_filename (real_filename);

ALTER TABLE phpbb_attachments
ADD FULLTEXT KEY file_name_and_description (attach_comment, real_filename);

ALTER TABLE `phpbb_attachments` 
ADD COLUMN `draft_id` MEDIUMINT(8) NULL AFTER `thumbnail`,
ADD INDEX `draft_id` (`draft_id` ASC) VISIBLE;

ALTER TABLE `phpbb_attachments` 
ADD COLUMN `order_in_post` MEDIUMINT(8) NULL DEFAULT NULL AFTER `draft_id`;

