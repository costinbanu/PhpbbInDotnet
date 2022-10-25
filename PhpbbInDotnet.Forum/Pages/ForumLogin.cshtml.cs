using CryptSharp.Core;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ForumLoginModel : PageModel
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

        public string Language { get; private set; } = Constants.DEFAULT_LANGUAGE;

        public ITranslationProvider TranslationProvider { get; }

        public ForumLoginModel(IForumDbContext context, ITranslationProvider translationProvider, IAppCache cache)
        {
            _context = context;
            TranslationProvider = translationProvider;
            _cache = cache;
        }

        public async Task<IActionResult> OnGet()
        {
            Language = TranslationProvider.GetLanguage();
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
            Language = TranslationProvider.GetLanguage();
            var forum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(filter => filter.ForumId == ForumId);

            if (forum == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError(nameof(Password), TranslationProvider.Errors[Language, "MISSING_REQUIRED_FIELD"]);
                return Page();
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
                ModelState.AddModelError(nameof(Password), TranslationProvider.Errors[Language, "WRONG_PASS"]);
                return await OnGet();
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