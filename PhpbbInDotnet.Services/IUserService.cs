using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IUserService
    {
        AuthenticatedUser DbUserToAuthenticatedUserBase(PhpbbUsers dbUser);
        Task<(string Message, bool? IsSuccess)> DeletePrivateMessage(int messageId);
        Task<(string Message, bool? IsSuccess)> EditPrivateMessage(int messageId, string subject, string text);
        Task<PhpbbUsers> GetAnonymousDbUser();
        Task<AuthenticatedUser> GetAuthenticatedUserById(int userId);
        Task<HashSet<int>> GetFoes(int userId);
        Task<IEnumerable<PhpbbGroups>> GetAllGroups();
        Task<HashSet<AuthenticatedUserExpanded.Permissions>> GetPermissions(int userId);
        Task<IEnumerable<PhpbbRanks>> GetAllRanks();
        Task<PhpbbGroups> GetUserGroup(int userId);
        Task<List<KeyValuePair<string, int>>> GetUserMap();
        Task<int?> GetUserRole(AuthenticatedUserExpanded user);
        Task<IEnumerable<KeyValuePair<string, string>>> GetUsers();
        Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy();
        Task<(string Message, bool? IsSuccess)> HidePrivateMessages(int userId, params int[] messageIds);
        Task<bool> IsAdmin(AuthenticatedUserExpanded user);
        Task<bool> IsUserModeratorInForum(AuthenticatedUserExpanded user, int forumId);
        Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext);
        Task<int> GetUnreadPMCount(int userId);
    }
}