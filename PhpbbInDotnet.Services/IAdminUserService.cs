using Microsoft.AspNetCore.Mvc.Rendering;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IAdminUserService
    {
        Task<(string Message, bool? IsSuccess)> BanUser(List<UpsertBanListDto> banlist, List<int> indexesToRemove, int adminUserId);
        Task<(string Message, bool? IsSuccess)> DeleteUsersWithEmailNotConfirmed(int[] userIds, int adminUserId);
        Task<List<UpsertBanListDto>> GetBanList();
        Task<List<UpsertGroupDto>> GetGroups();
        Task<List<PhpbbUsers>> GetInactiveUsers();
        Task<List<PhpbbUsers>> GetActiveUsersWithUnconfirmedEmail();
        Task<List<SelectListItem>> GetRanksSelectListItems();
        Task<List<SelectListItem>> GetRolesSelectListItems();
        Task<(string Message, bool? IsSuccess)> ManageGroup(UpsertGroupDto dto, int adminUserId);
        Task<(string Message, bool? IsSuccess)> ManageRank(int? rankId, string rankName, bool? deleteRank, int adminUserId);
        Task<(string Message, bool? IsSuccess)> ManageUser(AdminUserActions? action, int? userId, int adminUserId);
        Task<(string? Message, bool IsSuccess, List<PhpbbUsers> Result)> UserSearchAsync(AdminUserSearch? searchParameters);
    }
}