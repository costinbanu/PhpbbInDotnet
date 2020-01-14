using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class SearchModel : ModelWithPagination
    {
        public IConfiguration Config => _config;

        public Utils Utils => _utils;

        [BindProperty(SupportsGet = true)]
        public string QueryString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Author { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchText { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TotalResults { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? DoSearch { get; set; }

        public List<KeyValuePair<string, string>> Users { get; set; }

        public IEnumerable<ExtendedPostDisplay> Posts { get; private set; }

        public SearchModel(IConfiguration config, Utils utils) : base(config, utils) { }

        public async Task OnGet()
        {
            NameValueCollection query = null;

            if (!string.IsNullOrWhiteSpace(QueryString))
            {
                query = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(QueryString));
            }

            if (ForumId == null && query != null)
            {
                ForumId = int.TryParse(query["ForumId"], out var i) ? i as int? : null;
            }

            if (TopicId == null && query != null)
            {
                TopicId = int.TryParse(query["TopicId"], out var i) ? i as int? : null;
            }

            using (var context = new ForumDbContext(_config))
            {
                Users = await (
                    from u in context.PhpbbUsers
                    where u.UserId != 1 && u.UserType != 2
                    orderby u.Username
                    select KeyValuePair.Create(u.Username, u.Username)
                ).ToListAsync();
            }

            if (DoSearch ?? false)
            {
                await Search();
            }
        }

        public async Task OnPost()
        {
            await OnGet();
            await Search();
        }

        public string GetSearchLinkForPage(int page) =>
            $"/Search" +
                $"?{nameof(QueryString)}={HttpUtility.UrlEncode(QueryString)}" +
                $"&{nameof(Author)}={Author}" +
                $"&{nameof(ForumId)}={ForumId}" +
                $"&{nameof(TopicId)}={TopicId}" +
                $"&{nameof(SearchText)}={HttpUtility.UrlEncode(SearchText)}" +
                $"&{nameof(PageNum)}={page}" +
                $"&{nameof(TotalResults)}={TotalResults}" +
                $"&{nameof(DoSearch)}={true}";

        private async Task Search()
        {
            using (var context = new ForumDbContext(_config))
            using (var connection = context.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                DefaultTypeMap.MatchNamesWithUnderscores = true;
                var authorUser = await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UsernameClean == _utils.CleanString(Author));
                using (
                    var multi = await connection.QueryMultipleAsync(
                        "CALL `forum`.`search_post_text`(@forum, @topic, @author, @page, @search);",
                        new
                        {
                            forum = ForumId > 0 ? ForumId : null,
                            topic = TopicId > 0 ? TopicId : null,
                            author = authorUser?.UserId,
                            page = PageNum ?? 1,
                            search = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText)
                        }
                    )
                )
                {

                    Posts = await multi.ReadAsync<ExtendedPostDisplay>();
                    Parallel.ForEach(Posts, p =>
                    {
                        p.AuthorHasAvatar = !string.IsNullOrWhiteSpace(p.UserAvatar);
                        p.AuthorSignature = p.UserSig == null ? null : _utils.BbCodeToHtml(p.UserSig, p.UserSigBbcodeUid);
                    });
                    await _utils.ProcessPosts(Posts, PageContext, HttpContext, false);
                    PageNum = (await multi.ReadAsync<int>()).Single();
                    TotalResults = unchecked((int)(await multi.ReadAsync<long>()).Single());
                }

                await ComputePagination(TotalResults.Value, PageNum.Value, GetSearchLinkForPage(PageNum.Value + 1));
            }
        }

        public class ExtendedPostDisplay : PostDisplay
        {
            public string UserAvatar { get; set; }
            public string UserSig { get; set; }
            public string UserSigBbcodeUid { get; set; }
        }
    }
}