using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class AdminForumService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;
        private readonly ForumTreeService _forumService;

        public AdminForumService(IConfiguration config, Utils utils, ForumTreeService forumService)
        {
            _config = config;
            _utils = utils;
            _forumService = forumService;
        }

        public async Task<(string Message, bool? IsSuccess)> ManageForumsAsync(
            int? forumId, string forumName, string forumDesc, bool? hasPassword, string forumPassword, int? parentId,
            ForumType? forumType, List<int> childrenForums, Dictionary<AclEntityType, Dictionary<int, int>> rolesForAclEntity)
        {
            using (var context = new ForumDbContext(_config))
            {
                var actual = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == forumId);
                if (string.IsNullOrWhiteSpace(forumName))
                {
                    return ("Numele forumului nu este valid!", false);
                }

                actual.ForumName = forumName;
                actual.ForumDesc = forumDesc;
                if(hasPassword.HasValue && !hasPassword.Value)
                {
                    actual.ForumPassword = string.Empty;
                }
                if(!string.IsNullOrWhiteSpace(forumPassword))
                {
                    actual.ForumPassword = Crypter.Phpass.Crypt(forumPassword, Crypter.Phpass.GenerateSalt());
                }
                actual.ParentId = parentId ?? actual.ParentId;
                actual.ForumType = forumType ?? actual.ForumType;

                var children = await (
                    from f in context.PhpbbForums
                    where f.ParentId == forumId
                    orderby f.LeftId
                    select f
                ).ToListAsync();

                if (!children.Select(s => s.ForumId).SequenceEqual(childrenForums))
                {
                    children.ForEach(c => c.LeftId = (childrenForums.IndexOf(c.ForumId) + 1) * 2);
                }

                var userPermissions = await (
                    from p in context.PhpbbAclUsers
                    where p.ForumId == forumId
                       && rolesForAclEntity[AclEntityType.User].Keys.Contains(p.UserId)
                    select p
                ).ToListAsync();
                userPermissions.ForEach(p => p.AuthRoleId = rolesForAclEntity[AclEntityType.User][p.UserId]);

                var groupPermissions = await (
                    from p in context.PhpbbAclGroups
                    where p.ForumId == forumId
                       && rolesForAclEntity[AclEntityType.Group].Keys.Contains(p.GroupId)
                    select p
                ).ToListAsync();
                groupPermissions.ForEach(p => p.AuthRoleId = rolesForAclEntity[AclEntityType.Group][p.GroupId]);

                //await context.SaveChangesAsync();

                return ($"Forumul {forumName} a fost actualizat cu succes!", true);
            }
        }

        public async Task<(PhpbbForums Forum, List<PhpbbForums> Children)> ShowForum(int forumId)
        {
            using (var context = new ForumDbContext(_config))
            {
                return (
                    await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == forumId),
                    await (
                        from f in context.PhpbbForums
                        where f.ParentId == forumId
                        orderby f.LeftId
                        select f
                    ).ToListAsync()
                );
            }
        }

        public async Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, int forumId)
            => _forumService.GetPathInTree(
                await _forumService.GetForumTreeAsync(), 
                forum => new SelectListItem(forum.Name, forum.Id.ToString(), forum.Id == parentId, forum.Id == parentId || forum.Id == forumId || forum.ParentId == forumId),
                (item, level) => item.Text = $"{new string('-', level)} {item.Text}"
            );

        public async Task<IEnumerable<ForumPermissions>> GetPermissions(int forumId)
        {
            using (var context = new ForumDbContext(_config))
            using (var connection = context.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                DefaultTypeMap.MatchNamesWithUnderscores = true;
                return await connection.QueryAsync<ForumPermissions>("CALL `forum`.`get_forum_permissions`(@forumId);", new { forumId });
            }
        }
    }
}
