using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IUserService
    {
        AuthenticatedUserExpanded? ClaimsPrincipalToAuthenticatedUser(ClaimsPrincipal claimsPrincipal);
        AuthenticatedUser DbUserToAuthenticatedUserBase(PhpbbUsers dbUser);
        Task<ClaimsPrincipal> DbUserToClaimsPrincipal(PhpbbUsers user);
        Task<(string Message, bool? IsSuccess)> DeletePrivateMessage(int messageId);
        Task<(string Message, bool? IsSuccess)> EditPrivateMessage(int messageId, string subject, string text);
        Task<ClaimsPrincipal> GetAnonymousClaimsPrincipal();
        Task<PhpbbUsers> GetAnonymousDbUser();
        Task<AuthenticatedUser> GetAuthenticatedUserById(int userId);
        Task<HashSet<int>> GetFoes(int userId);
        Task<IEnumerable<PhpbbGroups>> GetGroupList();
        Task<HashSet<AuthenticatedUserExpanded.Permissions>> GetPermissions(int userId);
        Task<IEnumerable<PhpbbRanks>> GetRankList();
        Task<PhpbbGroups> GetUserGroup(int userId);
        Task<List<KeyValuePair<string, int>>> GetUserMap();
        Task<int?> GetUserRole(AuthenticatedUserExpanded user);
        Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy();
        Task<bool> HasPrivateMessagePermissions(int userId);
        Task<(string Message, bool? IsSuccess)> HidePrivateMessages(int userId, params int[] messageIds);
        Task<bool> IsUserAdminInForum(AuthenticatedUserExpanded? user, int forumId);
        Task<bool> IsUserModeratorInForum(AuthenticatedUserExpanded? user, int forumId);
        Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext);
        Task<int> UnreadPMs(int userId);
    }
}