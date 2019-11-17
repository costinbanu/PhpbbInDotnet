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
        [BindProperty(SupportsGet = true)]
        public int? AuthorId { get; set; }
        [BindProperty(SupportsGet = false), DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? DateFrom { get; set; }
        [BindProperty(SupportsGet = false), DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? DateTo { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? ForumId { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }
        [BindProperty(SupportsGet = false)]
        public string SearchText { get; set; }

        public List<KeyValuePair<string, int>> Users { get; set; }
        public List<PostDisplay> Posts { get; private set; }
        public SearchModel(IConfiguration config, Utils utils) : base(config, utils) { }

        public async Task OnGet()
        {
            using (var context = new forumContext(_config))
            {
                Users = await (from u in context.PhpbbUsers
                               where u.UserId != 1 && u.UserType != 2
                               orderby u.Username
                               select KeyValuePair.Create(u.Username, u.UserId))
                              .ToListAsync();
            }
        }

        public async Task<IActionResult> OnPost(/*int? forum, int? topic, int? author, string text*/)
        {
            var query = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(QueryString ?? string.Empty));

            using (var context = new forumContext(_config))
            using (var cmd = context.Database.GetDbConnection().CreateCommand())
            {
                await cmd.Connection.OpenAsync();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "forum.search_post_text";
                cmd.Parameters.AddRange(new[]
                {
                    new MySqlParameter("forum", /*int.TryParse(query.Get("forumId"), out var f) ? f : (int?)null*/ForumId > 0 ? ForumId : null),
                    new MySqlParameter("topic", /*int.TryParse(query.Get("topicId"), out var t) ? t : (int?)null*/TopicId > 0 ? TopicId : null),
                    new MySqlParameter("author", AuthorId),
                    new MySqlParameter("fromDate", DateFrom?.LocalTimeToTimestamp()),
                    new MySqlParameter("toDate", DateTo?.LocalTimeToTimestamp()),
                    new MySqlParameter("search", SearchText)
                });
                using (var reader = await cmd.ExecuteReaderAsync())
                using (var table = new DataTable())
                {
                    table.Load(reader);
                    Posts = table.AsEnumerable().Select(
                        tbl => new PostDisplay
                        {
                            PostTitle = tbl["post_subject"].ToString(),
                            PostText = tbl["post_text"].ToString()
                        }
                    ).ToList();
                }
            }

            //TODO: show selected value in forum tree

            return Page();
        }
    }
}