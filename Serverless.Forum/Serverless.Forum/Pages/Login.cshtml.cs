using CryptSharp.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
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
        private readonly CacheService _cacheService;
        private readonly UserService _userService;

        public string UserName { get; set; }

        public string Password { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        public string ErrorMessage { get; set; }

        public LoginModel(IConfiguration config, Utils utils, CacheService cacheService, UserService userService)
        {
            _config = config;
            _utils = utils;
            _cacheService = cacheService;
            _userService = userService;
        }

        public async Task<IActionResult> OnPost()
        {
            using (var context = new ForumDbContext(_config))
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
                else if(user.First().UserInactiveReason != UserInactiveReason.NotInactive || user.First().UserInactiveTime != 0)
                {
                    ErrorMessage = "Utilizatorul nu este activat!";
                    return Page();
                }
                else
                {
                    var currentUser = user.First();

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await _userService.DbUserToClaimsPrincipalAsync(currentUser),
                        new AuthenticationProperties
                        {
                            AllowRefresh = true,
                            ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                            IsPersistent = true,
                        });

                    var key = $"UserMustLogIn_{currentUser.UsernameClean}";
                    if (await _cacheService.GetFromCacheAsync<bool?>(key) ?? false)
                    {
                        await _cacheService.RemoveFromCacheAsync(key);
                    }

                    return Redirect(HttpUtility.UrlDecode(ReturnUrl));
                }
            }
        }
    }
}