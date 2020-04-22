using CryptSharp.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using System.Linq;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ForumLoginModel : PageModel
    {
        private readonly ForumDbContext _context;

        public string ReturnUrl { get; set; }

        public int ForumId { get; set; }

        public string ForumName { get; set; }

        public string ErrorMessage { get; set; }

        public ForumLoginModel(ForumDbContext context)
        {
            _context = context;
        }

        public void OnGet(string returnUrl, int forumId, string forumName)
        {
            ReturnUrl = returnUrl;
            ForumId = forumId;
            ForumName = forumName;
        }

        public IActionResult OnPost(string password, string returnUrl, int forumId)
        {
            var forum = from f in _context.PhpbbForums.AsNoTracking()
                        let cryptedPass = Crypter.Phpass.Crypt(password, f.ForumPassword)
                        where f.ForumId == forumId && cryptedPass == f.ForumPassword
                        select f;

            if (forum.Count() != 1)
            {
                ErrorMessage = "Numele de utilizator și/sau parola sunt greșite!";
                return Page();
            }
            else
            {
                HttpContext.Session.SetInt32($"ForumLogin_{forumId}", 1);
                return Redirect(HttpUtility.UrlDecode(returnUrl));
            }
        }
    }
}