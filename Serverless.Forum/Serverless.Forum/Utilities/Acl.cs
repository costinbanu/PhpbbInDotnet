using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Utilities
{
    public class Acl
    {
        static Acl _instance = null;
        LoggedUser _anonymous = null;

        public LoggedUser LoggedUserFromDbUser(PhpbbUsers user, forumContext _dbContext)
        {
            var groups = (from g in _dbContext.PhpbbUserGroup
                         where g.UserId == user.UserId
                         select g.GroupId).ToList();

            var userPermissions = (from up in _dbContext.PhpbbAclUsers
                                  where up.UserId == user.UserId
                                  select up).ToList();

            var groupPermissions = (from gp in _dbContext.PhpbbAclGroups
                                   where groups.Contains(gp.GroupId)
                                   select gp).ToList();

            var topicPostsPerPage = from tpp in _dbContext.PhpbbUserTopicPostNumber
                                    where tpp.UserId == user.UserId
                                    select KeyValuePair.Create(tpp.TopicId, tpp.PostNo);

            return new LoggedUser
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
                                   }).ToList().Union(
                                    from gp in groupPermissions
                                    select new LoggedUser.Permissions
                                    {
                                        ForumId = gp.ForumId,
                                        AuthOptionId = gp.AuthOptionId,
                                        AuthRoleId = gp.AuthRoleId,
                                        AuthSetting = gp.AuthSetting
                                    }).ToList(),
                TopicPostsPerPage = topicPostsPerPage.ToDictionary(k => k.Key, v => v.Value),
                UserDateFormat = user.UserDateformat
            };
        }

        public static Acl Instance => _instance ?? (_instance = new Acl());

        public LoggedUser GetAnonymousUser(forumContext _dbContext)
        {
            if (_anonymous != null)
            {
                return _anonymous;
            }

            _anonymous = LoggedUserFromDbUser(_dbContext.PhpbbUsers.First(u => u.UserId == 1), _dbContext);

            return _anonymous;
        }
    }
}
