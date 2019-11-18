using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    //[BindProperties, ValidateAntiForgeryToken]
    public class SearchModel : ModelWithLoggedUser
    {
        public IConfiguration Config => _config;
        public Utils Utils => _utils;

        [BindProperty(SupportsGet = true)]
        public string QueryString { get; set; }
        [BindProperty]
        public int? AuthorId { get; set; }
        [BindProperty]
        public int? ForumId { get; set; }
        [BindProperty]
        public int? TopicId { get; set; }
        [BindProperty]
        public string SearchText { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? PageNumber { get; set; }
        [BindProperty]
        public int? TotalResults { get; set; }

        public List<KeyValuePair<string, int>> Users { get; set; }
        public List<PostDisplay> Posts { get; private set; }
        public SearchModel(IConfiguration config, Utils utils) : base(config, utils) { }

        public async Task OnGet()
        {
            var query = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(QueryString ?? string.Empty));
            ForumId = int.TryParse(query["ForumId"], out var i) ? i as int? : null;
            TopicId = int.TryParse(query["TopicId"], out i) ? i as int? : null;

            using (var context = new forumContext(_config))
            {
                Users = await (
                    from u in context.PhpbbUsers
                    where u.UserId != 1 && u.UserType != 2
                    orderby u.Username
                    select KeyValuePair.Create(u.Username, u.UserId)
                ).ToListAsync();
            }
        }

        public async Task OnPost()
        {
            await OnGet();

            using (var context = new forumContext(_config))
            using (var connection = context.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                DefaultTypeMap.MatchNamesWithUnderscores = true;

                using (
                    var multi = await connection.QueryMultipleAsync(
                        "CALL `forum`.`search_post_text`(@forum, @topic, @author, @page, @search);",
                        new
                        {
                            forum = ForumId > 0 ? ForumId : null,
                            topic = TopicId > 0 ? TopicId : null,
                            author = AuthorId,
                            page = PageNumber ?? 1,
                            search = SearchText
                        }
                    )
                )
                {
                    todo: fix selecting a topic in the list (it will revert top whatever was selected before and it won't use it in a new search)
                    todo: add pagination

                    Posts = multi.Read<PostDisplay>().ToList();
                    Parallel.ForEach(Posts, (p, state) =>
                    {
                        p.PostSubject = HttpUtility.HtmlDecode(p.PostSubject);
                        p.PostText = HttpUtility.HtmlDecode(_utils.BBCodeParser.ToHtml(p.PostText, p.BbcodeUid));
                        p.PostText = _utils.NewLineRegex.Replace(p.PostText, "<br/>");
                        p.PostText = _utils.HtmlCommentRegex.Replace(p.PostText, string.Empty);
                        p.PostText = _utils.SmileyRegex.Replace(p.PostText, Constants.SMILEY_PATH);
                    });
                    PageNumber = multi.Read<int>().Single();
                    TotalResults = multi.Read<int>().Single();
                }
            }
        }
    }
}