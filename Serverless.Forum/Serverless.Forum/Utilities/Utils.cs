using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Serverless.Forum.Utilities
{
    public class Utils
    {
        static Utils _instance = null;
        ClaimsPrincipal _anonymous = null;

        public async Task<ClaimsPrincipal> LoggedUserFromDbUser(PhpbbUsers user, forumContext dbContext)
        {
            var groups = (from g in dbContext.PhpbbUserGroup
                          where g.UserId == user.UserId
                          select g.GroupId).ToList();

            var userPermissions = (from up in dbContext.PhpbbAclUsers
                                   where up.UserId == user.UserId
                                   select up).ToList();

            var groupPermissions = (from gp in dbContext.PhpbbAclGroups
                                    let alreadySet = from up in userPermissions
                                                     select up.ForumId
                                    where groups.Contains(gp.GroupId)
                                       && !alreadySet.Contains(gp.ForumId)
                                    select gp).ToList();

            var topicPostsPerPage = (from tpp in dbContext.PhpbbUserTopicPostNumber
                                     where tpp.UserId == user.UserId
                                     select KeyValuePair.Create(tpp.TopicId, tpp.PostNo)).ToList();

            var intermediary = new LoggedUser
            {
                UserId = user.UserId,
                Username = user.Username,
                UsernameClean = user.UsernameClean,
                Groups = groups,
                UserPermissions = (from up in userPermissions
                                   select new LoggedUser.Permissions
                                   {
                                       ForumId = up.ForumId,
                                       AuthOptionId = up.AuthOptionId,
                                       AuthRoleId = up.AuthRoleId,
                                       AuthSetting = up.AuthSetting
                                   })
                                   .ToList()
                                   .Union((from gp in groupPermissions
                                           select new LoggedUser.Permissions
                                           {
                                               ForumId = gp.ForumId,
                                               AuthOptionId = gp.AuthOptionId,
                                               AuthRoleId = gp.AuthRoleId,
                                               AuthSetting = gp.AuthSetting
                                           }).ToList()).ToList(),
                TopicPostsPerPage = topicPostsPerPage.ToDictionary(k => k.Key, v => v.Value),
                UserDateFormat = user.UserDateformat
            };

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.UserData, await CompressObject(intermediary)));
            return new ClaimsPrincipal(identity);
        }

        public static Utils Instance => _instance ?? (_instance = new Utils());

        public async Task<ClaimsPrincipal> GetAnonymousUser(forumContext _dbContext)
        {
            if (_anonymous != null)
            {
                return _anonymous;
            }

            _anonymous = await LoggedUserFromDbUser(
                await _dbContext.PhpbbUsers.FirstAsync(u => u.UserId == 1), 
                _dbContext);

            return _anonymous;
        }

        public IEnumerable<PhpbbPosts> GetPosts(int topicId, forumContext dbContext)
        {
            return from pp in dbContext.PhpbbPosts
                   where pp.TopicId == topicId
                   orderby pp.PostTime ascending
                   select pp;
        }

        public async Task<string> CompressObject<T>(T source)
        {
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source))))
            using (var memory = new MemoryStream())
            using (var gzip = new GZipStream(memory, CompressionMode.Compress))
            {
                await content.CopyToAsync(gzip);
                await gzip.FlushAsync();
                return Convert.ToBase64String(memory.ToArray());
            }
        }

        public async Task<T> DecompressObject<T>(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return default;
            }
            using (var content = new MemoryStream())
            using (var memory = new MemoryStream(Convert.FromBase64String(source)))
            using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
            {
                await gzip.CopyToAsync(content);
                await content.FlushAsync();
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(content.ToArray()));
            }
        }
    }
}
