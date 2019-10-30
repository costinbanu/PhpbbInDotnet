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
    [BindProperties]
    [ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public string UserName { get; set; }

        public string Password { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        public string ErrorMessage { get; set; }

        public LoginModel(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
        }

        public async Task<IActionResult> OnPost()
        {
            using (var context = new forumContext(_config))
            {
                var user = from u in context.PhpbbUsers
                           let cryptedPass = Crypter.Phpass.Crypt(Password, u.UserPassword)
                           where u.UsernameClean == _utils.CleanString(UserName) 
                              && cryptedPass == u.UserPassword
                           select u;

                if (user.Count() != 1)
                {
                    ErrorMessage = "Numele de utilizator și/sau parola sunt greșite!";
                    return Page();
                }
                else
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await _utils.LoggedUserFromDbUserAsync(user.First()),
                        new AuthenticationProperties
                        {
                            AllowRefresh = true,
                            ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                            IsPersistent = true,
                        });
                    return Redirect(HttpUtility.UrlDecode(ReturnUrl));
                }
            }
        }
    }
}