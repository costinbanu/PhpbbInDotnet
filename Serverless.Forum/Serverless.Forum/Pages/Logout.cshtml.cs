﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly UserService _userService;

        public LogoutModel(UserService userService)
        {
            _userService = userService;
        }

        public async Task<IActionResult> OnGet(string returnUrl)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                await _userService.GetAnonymousClaimsPrincipalAsync(), 
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