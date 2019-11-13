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
    [BindProperties, ValidateAntiForgeryToken]
    public class SearchModel : PageModel
    {
        private readonly Utils _utils;
        private readonly IConfiguration _config;

        [BindProperty(SupportsGet = true)]
        public string QueryString { get; set; }

        public string SearchText { get; set; }

        public string AuthorName { get; set; }

        [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime DateFrom { get; set; }

        [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:dd.MM.yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime DateTo { get; set; }

        public List<PostDisplay> Posts { get; private set; }

        public SearchModel(Utils utils, IConfiguration config)
        {
            _utils = utils;
            _config = config;
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
                    new MySqlParameter("forum", int.TryParse(query.Get("forumId"), out var f) ? f : (int?)null),
                    new MySqlParameter("topic", int.TryParse(query.Get("topicId"), out var t) ? t : (int?)null),
                    new MySqlParameter("author", /*author*/(int?)null),
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

            return Page();
        }
    }
}