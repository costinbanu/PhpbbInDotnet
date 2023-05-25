using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using System;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
	public class LogoutModel : BaseModel
    {
        public LogoutModel(IConfiguration config, ITranslationProvider translationProvider, IUserService userService)
            : base(translationProvider, userService, config)
        {
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
                    ExpiresUtc = DateTimeOffset.Now.Add(Configuration.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30)),
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