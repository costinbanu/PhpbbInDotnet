using CryptSharp.Core;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ForumLoginModel : BaseModel
    {
        private readonly IForumDbContext _context;
        private readonly IAppCache _cache;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        public string? ForumName { get; private set; }

        public ForumLoginModel(IForumDbContext context, ITranslationProvider translationProvider, IAppCache cache)
            : base(translationProvider)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IActionResult> OnGet()
        {
            var forum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(filter => filter.ForumId == ForumId);

            if (forum == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(forum.ForumPassword))
            {
                if (string.IsNullOrWhiteSpace(ReturnUrl))
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                else
                {
                    return Redirect(HttpUtility.UrlDecode(ReturnUrl));
                }
            }
            ForumName = HttpUtility.HtmlDecode(forum.ForumName ?? string.Empty);
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var forum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(filter => filter.ForumId == ForumId);

            if (forum == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                return PageWithError(nameof(Password), TranslationProvider.Errors[Language, "MISSING_REQUIRED_FIELD"]);
            }
                       
            if (forum == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(forum.ForumPassword))
            {
                if (string.IsNullOrWhiteSpace(ReturnUrl))
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                else
                {
                    return Redirect(HttpUtility.UrlDecode(ReturnUrl));
                }
            }

            if (forum.ForumPassword != Crypter.Phpass.Crypt(Password, forum.ForumPassword))
            {
                return await PageWithErrorAsync(nameof(Password), TranslationProvider.Errors[Language, "WRONG_PASS"], OnGet);
            }
            else
            {
                var userId = AuthenticatedUserExpanded.GetValue(HttpContext).UserId;
                _cache.Add(CacheUtility.GetForumLoginCacheKey(userId, ForumId), 1, TimeSpan.FromMinutes(30));
                if (string.IsNullOrWhiteSpace(ReturnUrl))
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                return Redirect(HttpUtility.UrlDecode(ReturnUrl));
            }
        }
    }
}