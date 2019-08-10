using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages
{
    public class LoginModel : PageModel
    {
        public string username;
        public string password;
        public bool rememberMe;
        public string returnUrl;

        public void OnGet(string ReturnUrl)
        {
            returnUrl = ReturnUrl;
        }

        public void OnPost(string username, string password, string returnUrl, bool rememberMe)
        {
            var x = HttpContext.Request.Query["ReturnUrl"];
        }
    }
}