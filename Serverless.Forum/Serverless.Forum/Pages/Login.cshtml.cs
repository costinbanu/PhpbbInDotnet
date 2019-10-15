using CryptSharp.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;
        public string returnUrl;
        public string errorMessage;

        public LoginModel(IConfiguration config)
        {
            _config = config;
        }

        public void OnGet(string ReturnUrl)
        {
            returnUrl = ReturnUrl;
        }

        public async Task<IActionResult> OnPost(string username, string password, string returnUrl, bool rememberMe = false)
        {
            using (var _dbContext = new forumContext(_config))
            {
                var user = from u in _dbContext.PhpbbUsers
                           let cryptedPass = Crypter.Phpass.Crypt(password, u.UserPassword)
                           where u.UsernameClean == username && cryptedPass == u.UserPassword
                           select u;

                if (user.Count() != 1)
                {
                    errorMessage = "Numele de utilizator și/sau parola sunt greșite!";
                    return Page();
                }
                else
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await Utils.Instance.LoggedUserFromDbUser(user.First(), _dbContext),
                        new AuthenticationProperties
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
}