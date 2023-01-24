using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PhpbbInDotnet.Objects
{
    public class AuthenticatedUserExpanded : AuthenticatedUser
    {
        public AuthenticatedUserExpanded(AuthenticatedUser @base)
        {
            UserId = @base.UserId;
            Username = @base.Username;
            UsernameClean = @base.UsernameClean;
            UserDateFormat = @base.UserDateFormat;
            UserColor = @base.UserColor;
            AllowPM = @base.AllowPM;
            JumpToUnread = @base.JumpToUnread;
            Language = @base.Language;
            EmailAddress = @base.EmailAddress;
            ShouldConfirmEmail= @base.ShouldConfirmEmail;
        }

        public AuthenticatedUserExpanded() { }

        public HashSet<Permissions>? AllPermissions { get; set; } = null;

        public Dictionary<int, int>? TopicPostsPerPage { get; set; } = null;

        public int PostEditTime { get; set; } = 60;

        public int? UploadLimit { get; set; }

        public HashSet<int>? Foes { get; set; }

        public string? Style { get; set; }

        public int GetPageSize(int topicId)
            => TopicPostsPerPage?.TryGetValue(topicId, out var pageSize) == true ? pageSize : Constants.DEFAULT_PAGE_SIZE;

        public bool IsForumRestricted(int forumId)
            => AllPermissions?.Contains(new Permissions { ForumId = forumId, AuthRoleId = Constants.FORUM_RESTRICTED_ROLE }) == true;

        public bool IsForumReadOnly(int forumId)
            => IsAnonymous || AllPermissions?.Contains(new Permissions { ForumId = forumId, AuthRoleId = Constants.FORUM_READONLY_ROLE }) == true;

        public static bool TryGetValue(HttpContext httpContext, [NotNullWhen(true)] out AuthenticatedUserExpanded? user)
        {
            if (httpContext.Items.TryGetValue(nameof(AuthenticatedUserExpanded), out var raw) && raw is AuthenticatedUserExpanded aue)
            {
                user = aue;
                return true;
            }
            user = null;
            return false;
        }

        public static AuthenticatedUserExpanded GetValue(HttpContext httpContext)
            => TryGetValue(httpContext, out var val) ? val : throw new InvalidOperationException($"No instance of {nameof(AuthenticatedUserExpanded)} found in the current {nameof(HttpContext)}.");

        public static AuthenticatedUserExpanded? GetValueOrDefault(HttpContext httpContext)
            => TryGetValue(httpContext, out var val) ? val : null;

        public void SetValue(HttpContext httpContext)
        {
            httpContext.Items[nameof(AuthenticatedUserExpanded)] = this;
        }

        public bool HasPrivateMessagePermissions
            => !IsAnonymous && AllPermissions?.Contains(new Permissions { ForumId = 0, AuthRoleId = Constants.NO_PM_ROLE }) != true;

        public bool HasPrivateMessages
            => AllowPM && HasPrivateMessagePermissions;

        public class Permissions
        {
            public int ForumId { get; set; } = 0;

            public int AuthOptionId { get; set; } = 0;

            public int AuthRoleId { get; set; } = 0;

            public int AuthSetting { get; set; } = 0;

            public override bool Equals(object? obj)
                => obj is Permissions perm && ForumId == perm.ForumId && AuthRoleId == perm.AuthRoleId;

            public override int GetHashCode()
                => HashCode.Combine(ForumId, AuthRoleId);
        }
    }
}
