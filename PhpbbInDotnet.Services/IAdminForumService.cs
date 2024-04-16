using Microsoft.AspNetCore.Mvc.Rendering;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IAdminForumService
    {
        Task<(string Message, bool? IsSuccess)> DeleteForum(int forumId, int adminUserId);
        Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, ForumUserExpanded? user);
        Task<IEnumerable<ForumPermissions>> GetPermissions(int forumId);
        Task<UpsertForumResult> ManageForumsAsync(UpsertForumDto dto, int adminUserId, bool isRoot);
        Task<(PhpbbForums? Forum, List<PhpbbForums> Children)> ShowForum(int forumId);
    }
}