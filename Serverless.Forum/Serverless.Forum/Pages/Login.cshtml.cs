using CryptSharp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Linq;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class LoginModel : PageModel
    {
        public string returnUrl;
        public string errorMessage;

        forumContext _dbContext;
        IHttpContextAccessor _httpContext;
        IDataProtector _dpProtector;

        public LoginModel(forumContext dbContext, IHttpContextAccessor httpContext, IDataProtectionProvider dpProvider)
        {
            _dbContext = dbContext;
            _httpContext = httpContext;
            _dpProtector = dpProvider.CreateProtector("Cookie Encryption");
        }

        public void OnGet(string ReturnUrl)
        {
            returnUrl = ReturnUrl;
        }

        public IActionResult OnPost(string username, string password, string returnUrl, bool rememberMe = false)
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
                var toSave = JsonConvert.SerializeObject(Acl.Instance.LoggedUserFromDbUser(user.First(), _dbContext));
                _httpContext.HttpContext.Session.SetString("user", toSave);
                //Response.Cookies.Append(
                //    /*_dpProtector.Protect(*/"user"/*)*/, 
                //    /*_dpProtector.Protect(*/toSave/*)*/, 
                //    new CookieOptions
                //    {
                //        Expires = DateTime.Now.AddDays(30),
                //        IsEssential = true
                //    }
                //);
                return Redirect(HttpUtility.UrlDecode(returnUrl));
            }
        }
    }
}