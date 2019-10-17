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
using System.Security.Cryptography;
using System.Globalization;

namespace Serverless.Forum.Utilities
{
    public class Utils
    {
        static Utils _instance = null;
        ClaimsPrincipal _anonymous = null;

        public async Task<ClaimsPrincipal> LoggedUserFromDbUser(PhpbbUsers user, forumContext dbContext)
        {
            var groups = await (from g in dbContext.PhpbbUserGroup
                                where g.UserId == user.UserId
                                select g.GroupId).ToListAsync();

            var userPermissions = await (from up in dbContext.PhpbbAclUsers
                                         where up.UserId == user.UserId
                                         select up).ToListAsync();

            var groupPermissions = await (from gp in dbContext.PhpbbAclGroups
                                          let alreadySet = from up in userPermissions
                                                           select up.ForumId
                                          where groups.Contains(gp.GroupId)
                                             && !alreadySet.Contains(gp.ForumId)
                                          select gp).ToListAsync();

            var topicPostsPerPage = await (from tpp in dbContext.PhpbbUserTopicPostNumber
                                           where tpp.UserId == user.UserId
                                           select KeyValuePair.Create(tpp.TopicId, tpp.PostNo)).ToListAsync();

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

        public string RandomString(int length = 8)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        public string CalculateMD5Hash(string input)
        {
            var hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public string CleanString(string input)
        {
            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().ToLower().Normalize(NormalizationForm.FormC);
        }
    }
}
