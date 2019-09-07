using CryptSharp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;
using System.Linq;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ForumLoginModel : PageModel
    {
        public string ReturnUrl { get; set; }

        public int ForumId { get; set; }

        public string ForumName { get; set; }

        public string ErrorMessage { get; set; }

        forumContext _dbContext;

        public ForumLoginModel(forumContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void OnGet(string ReturnUrl, int ForumId, string ForumName)
        {
            this.ReturnUrl = ReturnUrl;
            this.ForumId = ForumId;
            this.ForumName = ForumName;
        }

        public IActionResult OnPost(string password, string returnUrl, int forumId)
        {
            var forum = from f in _dbContext.PhpbbForums
                        let cryptedPass = Crypter.Phpass.Crypt(password, f.ForumPassword)
                        where f.ForumId == forumId && cryptedPass == f.ForumPassword
                        select f;

            if (forum.Count() != 1)
            {
                ErrorMessage = "Authentication error!";
                return Page();
            }
            else
            {
                HttpContext.Session.SetInt32("ForumLogin", forumId);
                return Redirect(HttpUtility.UrlDecode(returnUrl));
            }
        }
    }
}