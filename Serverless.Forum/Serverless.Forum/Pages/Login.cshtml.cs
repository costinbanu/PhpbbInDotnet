using CryptSharp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
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

        public LoginModel(forumContext dbContext, IHttpContextAccessor httpContext)
        {
            _dbContext = dbContext;
            _httpContext = httpContext;
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
                _httpContext.HttpContext.Session.SetString(
                    "user", 
                    JsonConvert.SerializeObject(
                        Acl.Instance.LoggedUserFromDbUser(user.First(), _dbContext)
                    )
                );
                return Redirect(HttpUtility.UrlDecode(returnUrl));
            }
        }
    }
}