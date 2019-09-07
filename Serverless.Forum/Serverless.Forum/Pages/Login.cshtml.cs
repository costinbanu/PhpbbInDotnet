using CryptSharp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class LoginModel : PageModel
    {
        public string returnUrl;
        public string errorMessage;

        forumContext _dbContext;

        public LoginModel(forumContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void OnGet(string ReturnUrl)
        {
            returnUrl = ReturnUrl;
        }

        public async Task<IActionResult> OnPost(string username, string password, string returnUrl, bool rememberMe = false)
        {
            var user = from u in _dbContext.PhpbbUsers
                       let cryptedPass = Crypter.Phpass.Crypt(password, u.UserPassword)
                       where u.UsernameClean == username && cryptedPass == u.UserPassword
                       select u;

            if (user.Count() != 1)
            {
                errorMessage = "Authentication error!";
                return Page();
            }
            else
            {
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Acl.Instance.LoggedUserFromDbUser(user.First(), _dbContext), new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                    IsPersistent = true,
                });
                return Redirect(HttpUtility.UrlDecode(returnUrl));
            }
        }
    }
}