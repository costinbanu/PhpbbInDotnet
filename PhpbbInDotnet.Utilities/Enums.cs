using System;

namespace PhpbbInDotnet.Utilities
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
        ChangedEmailConfirmed = 8
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
        RestoreTopic
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

    [Flags]
    public enum ViewForumMode
    {
        OwnPosts,
        NewPosts,
        Drafts,
        Forum = ~OwnPosts & ~NewPosts & ~Drafts
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
        Emoji
    }

    public enum RecycleBinItemType
    {
        Forum,
        Topic,
        Post
    }
}
