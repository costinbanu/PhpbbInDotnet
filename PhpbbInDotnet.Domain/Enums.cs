﻿using System;

namespace PhpbbInDotnet.Domain
{
    public enum UserInactiveReason
    {
        /// <summary>
        /// User is active
        /// </summary>
        NotInactive = 0,

        /// <summary>
        /// Registration pre-activation (did not confirm email)
        /// </summary>
        NewlyRegisteredNotConfirmed = 1,

        /// <summary>
        /// Profile change (e.g. e-mail address change) by user or an admin
        /// </summary>
        ProfileChange = 2,

        /// <summary>
        /// Admin deactivated
        /// </summary>
        InactivatedByAdmin = 3,

        /// <summary>
        /// Permanently Banned (considered different from "inactive")
        /// </summary>
        PermanentlyBanned = 4,

        /// <summary>
        /// Temporarily Banned (considered different from "inactive")
        /// </summary>
        TemporarilyBanned = 5,

        /// <summary>
        /// Registration pre-activation (confirmed email)
        /// </summary>
        NewlyRegisteredConfirmed = 6,

        /// <summary>
        /// User changed their email and have not confirmed it yet
        /// </summary>
        ChangedEmailNotConfirmed = 7,

        /// <summary>
        /// User changed their email and have confirmed it
        /// </summary>
        ChangedEmailConfirmed = 8,

        /// <summary>
        /// User changed their email and an admin has force-activated them
        /// </summary>
        Active_NotConfirmed = 9,
    }

    public enum ForumType
    {
        Category = 0,
        SubForum = 1
    }

    public enum AdminCategories
    {
        Users,
        Forums,
        WritingTools,
        Logs
    }

    public enum AdminUserActions
    {
        Activate,
        Activate_WithUnregisteredEmail,
        Deactivate,
        Delete_KeepMessages,
        Delete_DeleteMessages,
        Remind
    }

    public enum AdminGroupActions
    {
        Add,
        Delete,
        Update
    }

    public enum AdminRankActions
    {
        Add,
        Delete,
        Update
    }

    public enum AdminBanListActions
    {
        Add,
        Delete,
        Update
    }

    public enum AdminForumActions
    {
        Add,
        Delete,
        Update,
        Restore
    }

    public enum AclEntityType
    {
        User,
        Group
    }

    public enum TopicType
    {
        Normal = 0,
        Important = 1,
        Announcement = 2,
        Global = 3
    }

    public enum ModeratorTopicActions
    {
        LockTopic,
        UnlockTopic,
        MoveTopic,
        DeleteTopic,
        MakeTopicNormal,
        MakeTopicImportant,
        MakeTopicAnnouncement,
        MakeTopicGlobal,
        RestoreTopic,
        CreateShortcut,
        RemoveShortcut
    }

    public enum ModeratorPostActions
    {
        MoveSelectedPosts,
        SplitSelectedPosts,
        DeleteSelectedPosts,
        DuplicateSelectedPost,
        RestorePosts
    }

    public enum UserProfileActions
    {
        ChangeEmail,
        ChangePassword,
        ChangeUsername
    }

    public enum PostingActions
    {
        NewTopic,
        NewForumPost,
        EditForumPost,
        NewPrivateMessage,
        EditPrivateMessage
    }

    public enum PostingFontSize
    {
        Tiny = 50,
        Small = 85,
        Normal = 100,
        Large = 150,
        Huge = 200
    }

    public enum LoginMode
    {
        Normal,
        PasswordReset
    }

    public enum PrivateMessagesPages
    {
        Inbox,
        Sent,
        Message
    }

    public enum MemberListPages
    {
        AllUsers,
        SearchUsers,
        Groups,
        ActiveUsers,
        ActiveBots
    }

    public enum MemberListOrder
    {
        NameAsc,
        NameDesc,
        RegistrationDateAsc,
        RegistrationDateDesc,
        LastActiveDateAsc,
        LastActiveDateDesc,
        MessageCountAsc,
        MessageCountDesc
    }

    public enum UserPageMode
    {
        View,
        Edit,
        AddFoe,
        RemoveFoe,
        RemoveMultipleFoes
    }

    public enum ModeratorPanelMode
    {
        TopicModeration = 0,
        Reports,
        RecycleBin
    }

    public enum Casing
    {
        None,
        AllLower,
        AllUpper,
        FirstUpper,
        Title
    }

    public enum OperationLogType
    {
        Administrator = 0,
        Moderator = 1,
        Error = 2,
        User = 3
    }

    public enum FileType
    {
        Attachment,
        Avatar,
        Log
    }

    public enum RecycleBinItemType
    {
        Forum,
        Topic,
        Post
    }

    public enum StatisticsPeriod
    {
        TwentyFourHours,
        SevenDays,
        ThirtyDays,
        SixMonths,
        OneYear,
        AllTime
    }

    [Flags]
    public enum ForumUserExpansionType
    {
        Permissions = 1 << 0,
        TopicPostsPerPage = 1 << 1,
        UploadLimit = 1 << 2,
        PostEditTime = 1 << 3,
        Foes = 1 << 4,
        Style = 1 << 5,
    }

    public enum StorageType
    {
        HardDisk = 0,
        AzureStorage = 1
    }

    public enum DatabaseType
    {
        MySql = 0,
        SqlServer = 1
    }

    public enum SubscriptionPageMode
    {
        Forums,
        Topics
    }
}
