using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Services;
using System;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly IConfiguration _config;

        public LogoutModel(IConfiguration config)
        {
            _config = config;
        }

        public async Task<IActionResult> OnGet(string returnUrl)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                IdentityUtility.CreateClaimsPrincipal(Constants.ANONYMOUS_USER_ID), 
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.Add(_config.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30)),
                    IsPersistent = true,
                });

            if (string.IsNullOrWhiteSpace(returnUrl) ||
                (returnUrl.Contains("user", StringComparison.InvariantCultureIgnoreCase) && returnUrl.Contains("foe", StringComparison.InvariantCultureIgnoreCase)) ||
                returnUrl.Contains("logout", StringComparison.InvariantCultureIgnoreCase) ||
                returnUrl.Contains("register", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                returnUrl = "/";
            }

            return Redirect(HttpUtility.UrlDecode(returnUrl));
        }
    }
}