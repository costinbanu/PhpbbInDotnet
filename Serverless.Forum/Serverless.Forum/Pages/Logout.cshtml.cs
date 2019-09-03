﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class LogoutModel : PageModel
    {

        forumContext _dbContext;

        public LogoutModel(forumContext context)
        {
            _dbContext = context;
        }
        public async Task<IActionResult> OnGet(string returnUrl)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Acl.Instance.GetAnonymousUser(_dbContext), new AuthenticationProperties
            {
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                IsPersistent = true,
            });
            return Redirect(HttpUtility.UrlDecode(returnUrl));
        }
    }
}