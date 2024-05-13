using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class SubscriptionsModel : AuthenticatedPageModel
    {
        public List<ForumDto> ForumSubscriptions { get; private set; } = [];
        public List<TopicDto> TopicSubscriptions { get; private set; } = [];

        public SubscriptionsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {

        }

        public Task OnGet()
            => WithRegisteredUser(async curUser =>
            {
                ForumSubscriptions = (await SqlExecuter.QueryAsync<ForumDto>(
                    @"SELECT f.forum_id, 
                             f.forum_name, 
                             f.forum_desc, 
                             f.forum_last_post_id, 
                             f.forum_last_post_time, 
                             f.forum_last_poster_id, 
                             f.forum_last_poster_name, 
                             f.forum_last_poster_colour 
                        FROM phpbb_forums f
                        JOIN phpbb_forums_watch fw ON f.forum_id = fw.forum_id
                       WHERE user_id = @userId",
                    new { curUser.UserId })).AsList();

                TopicSubscriptions = (await SqlExecuter.QueryAsync<TopicDto>(
                    @"SELECT t.forum_id, 
                             t.topic_id, 
                             t.topic_title, 
                             t.topic_last_poster_id, 
                             t.topic_last_poster_name, 
                             t.topic_last_poster_colour, 
                             t.topic_last_post_time, 
                             count(p.post_id) AS post_count
                        FROM phpbb_topics t
                        JOIN phpbb_topics_watch tw ON t.topic_id = tw.topic_id
                        JOIN phpbb_posts p ON t.topic_id = p.topic_id
                       WHERE user_id = @userId
                       GROUP BY t.forum_id, t.topic_id, t.topic_title, t.topic_last_poster_id, t.topic_last_poster_name, t.topic_last_poster_colour, t.topic_last_post_time",
					new { curUser.UserId })).AsList();

                return Page();
			});
    }
}
