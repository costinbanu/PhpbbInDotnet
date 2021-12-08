using CryptSharp.Core;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ForumLoginModel : PageModel
    {
        private readonly ForumDbContext _context;
        private readonly UserService _userService;
        private readonly CommonUtils _utils;
        private readonly IAppCache _cache;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        public string? ForumName { get; private set; }

        public string Language { get; private set; } = Constants.DEFAULT_LANGUAGE;

        public LanguageProvider LanguageProvider { get; }

        public ForumLoginModel(ForumDbContext context, UserService userService, LanguageProvider languageProvider, CommonUtils utils, IAppCache cache)
        {
            _context = context;
            _userService = userService;
            LanguageProvider = languageProvider;
            _utils = utils;
            _cache = cache;
        }

        public async Task<IActionResult> OnGet()
        {
            Language = LanguageProvider.GetValidatedLanguage(_userService.ClaimsPrincipalToAuthenticatedUser(User), Request);
            var forum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(filter => filter.ForumId == ForumId);

            if (forum == null)
            {
                return RedirectToPage("Error", new { IsNotFound = true });
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
            Language = LanguageProvider.GetValidatedLanguage(_userService.ClaimsPrincipalToAuthenticatedUser(User), Request);
            var forum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(filter => filter.ForumId == ForumId);

            if (forum == null)
            {
                return RedirectToPage("Error", new { IsNotFound = true });
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError(nameof(Password), LanguageProvider.Errors[Language, "MISSING_REQUIRED_FIELD"]);
                return Page();
            }
                       
            if (forum == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
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
                ModelState.AddModelError(nameof(Password), LanguageProvider.Errors[Language, "WRONG_PASS"]);
                return await OnGet();
            }
            else
            {
                var userId = _userService.ClaimsPrincipalToAuthenticatedUser(User)?.UserId ?? Constants.ANONYMOUS_USER_ID;
                _cache.Add(_utils.GetForumLoginCacheKey(userId, ForumId), 1, TimeSpan.FromMinutes(30));
                if (string.IsNullOrWhiteSpace(ReturnUrl))
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                return Redirect(HttpUtility.UrlDecode(ReturnUrl));
            }
        }
    }
}