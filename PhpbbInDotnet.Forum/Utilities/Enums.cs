using System;

namespace PhpbbInDotnet.Forum.Utilities
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
        ChangedEmailNotConfirmed = 7
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
        WritingTools
    }

    public enum AdminUserActions
    {
        Activate,
        Deactivate,
        Delete_KeepMessages,
        Delete_DeleteMessages
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
        MakeTopicGlobal
    }

    public enum ModeratorPostActions
    {
        MoveSelectedPosts,
        SplitSelectedPosts,
        DeleteSelectedPosts
    }

    public enum PostingActions
    {
        NewTopic,
        NewForumPost,
        EditForumPost,
        NewPrivateMessage,
        EditPrivateMessage
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
        ActiveUsers
    }

    public enum MemberListOrder
    {
        NameAsc,
        NameDesc,
        RegistrationDateAsc,
        RegistrationDateDesc,
        LastActiveDateAsc,
        LastActiveDateDesc
    }

    [Flags]
    public enum ViewForumMode
    {
        OwnPosts,
        NewPosts,
        Drafts,
        Forum = ~OwnPosts & ~NewPosts & ~Drafts
    }
}
