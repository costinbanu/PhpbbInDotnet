using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class MemberListModel : ModelWithLoggedUser
    {
        
        const int PAGE_SIZE = 50;
        private readonly Utils _utils;
        private readonly IConfiguration _config;

        public List<PhpbbUsers> UserList { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string Username { get; set; }

        public Paginator Paginator { get; private set; }
        public bool IsSearch { get; private set; }

        public MemberListModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, Utils utils, IConfiguration config) 
            : base (context, forumService, userService, cacheService) 
        {
            _utils = utils;
            _config = config;
        }

        public async Task OnGet()
        {
            UserList = await _context.PhpbbUsers.AsNoTracking().OrderBy(u => u.UsernameClean).Skip(PAGE_SIZE * (PageNum - 1)).Take(PAGE_SIZE).ToListAsync();
            Paginator = new Paginator(await _context.PhpbbUsers.AsNoTracking().CountAsync(), PageNum, "/MemberList?action=none", PAGE_SIZE, "pageNum");
        }

        public async Task OnGetSearch()
        {
            IsSearch = true;
            var cleanedInput = _utils.CleanString(HttpUtility.UrlDecode(Username));
            //var results = await _context.PhpbbUsers.AsNoTracking().Where(u => u.UsernameClean.Contains(cleanedInput)).OrderBy(u => u.UsernameClean).ToListAsync();
            //if (!results.Any() && _config.GetValue<bool>("CompatibilityMode") && "ăîâșțĂÎÂȘȚ".Any(c => Username.Contains(c)))
            //{
               var results = (await _context.PhpbbUsers.AsNoTracking().ToListAsync()).Where(u => _utils.CleanString(u.Username).Contains(cleanedInput)).ToList();
            //}
            UserList = results.Skip(PAGE_SIZE * (PageNum - 1)).Take(PAGE_SIZE).ToList();
            Paginator = new Paginator(results.Count, PageNum, $"/MemberList?username={HttpUtility.UrlEncode(Username)}&handler=search", PAGE_SIZE, "pageNum");
        }
    }
}