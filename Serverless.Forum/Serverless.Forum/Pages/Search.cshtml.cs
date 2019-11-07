using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class SearchModel : PageModel
    {
        private readonly Utils _utils;
        private readonly IConfiguration _config;

        public List<PostDisplay> Posts { get; private set; }

        public SearchModel(Utils utils, IConfiguration config)
        {
            _utils = utils;
            _config = config;
        }

        public async Task<IActionResult> OnGet(int? forum, int? topic, int? author, string text)
        {
            using (var context = new forumContext(_config))
            using (var cmd = context.Database.GetDbConnection().CreateCommand())
            {
                await cmd.Connection.OpenAsync();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "forum.search_post_text";
                cmd.Parameters.AddRange(new[]
                {
                    new MySqlParameter("forum", forum),
                    new MySqlParameter("topic", topic),
                    new MySqlParameter("author", author),
                    new MySqlParameter("search", text)
                });
                using (var reader = await cmd.ExecuteReaderAsync())
                using (var table = new DataTable())
                {
                    table.Load(reader);
                    Posts = table.AsEnumerable().Select(
                        t => new PostDisplay
                        {
                            PostTitle = t["post_subject"].ToString(),
                            PostText = t["post_text"].ToString()
                        }
                    ).ToList();
                }
            }

            return Page();
        }
    }
}