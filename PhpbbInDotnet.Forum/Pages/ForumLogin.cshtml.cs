using CryptSharp.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ForumLoginModel : PageModel
    {
        private readonly ForumDbContext _context;

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty, Required(ErrorMessage = "Trebuie să introduci o parolă")]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public string ForumName { get; private set; }


        public ForumLoginModel(ForumDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGet()
        {
            ForumName = HttpUtility.HtmlDecode((await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId))?.ForumName ?? string.Empty);
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var forum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId);
                        
            if (forum == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }

            if (string.IsNullOrWhiteSpace(forum.ForumPassword))
            {
                return Redirect(HttpUtility.UrlDecode(ReturnUrl));
            }

            if (forum.ForumPassword != Crypter.Phpass.Crypt(Password, forum.ForumPassword))
            {
                ErrorMessage = "Numele de utilizator și/sau parola sunt greșite!";
                return Page();
            }
            else
            {
                HttpContext.Session.SetInt32($"ForumLogin_{ForumId}", 1);
                return Redirect(HttpUtility.UrlDecode(ReturnUrl));
            }
        }
    }
}