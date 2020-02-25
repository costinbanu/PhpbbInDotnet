﻿namespace Serverless.Forum.Utilities
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
        Logs
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
}
