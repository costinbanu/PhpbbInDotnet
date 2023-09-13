using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IUserService
    {
        ForumUser DbUserToForumUser(PhpbbUsers dbUser);
        Task<ForumUserExpanded> ExpandForumUser(ForumUser forumUser, ForumUserExpansionType expansionType);
        Task<(string Message, bool? IsSuccess)> DeletePrivateMessage(int messageId);
        Task<(string Message, bool? IsSuccess)> EditPrivateMessage(int messageId, string subject, string text);
        Task<PhpbbUsers> GetAnonymousDbUserAsync();
        ForumUserExpanded GetAnonymousForumUserExpanded();
        Task<ForumUserExpanded> GetAnonymousForumUserExpandedAsync();
        Task<ForumUser> GetForumUserById(int userId, ITransactionalSqlExecuter? transaction = null);
        Task<IEnumerable<PhpbbGroups>> GetAllGroups();
        Task<IEnumerable<PhpbbRanks>> GetAllRanks();
        Task<PhpbbGroups> GetUserGroup(int userId);
        Task<List<KeyValuePair<string, int>>> GetUserMap();
        Task<int?> GetUserRole(ForumUserExpanded user);
        Task<IEnumerable<KeyValuePair<string, string>>> GetUsers();
        Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy();
        Task<(string Message, bool? IsSuccess)> HidePrivateMessages(int userId, params int[] messageIds);
        Task<bool> IsAdmin(ForumUserExpanded user);
        Task<bool> IsUserModeratorInForum(ForumUserExpanded user, int forumId);
        Task<(string Message, bool? IsSuccess)> SendPrivateMessage(ForumUserExpanded sender, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext);
        Task<int> GetUnreadPMCount(int userId);
    }
}