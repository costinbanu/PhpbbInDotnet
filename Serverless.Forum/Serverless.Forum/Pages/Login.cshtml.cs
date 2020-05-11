using CryptSharp.Core;
using Force.Crc32;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [BindProperties]
    [ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly ForumDbContext _context;
        private readonly Utils _utils;
        private readonly CacheService _cacheService;
        private readonly UserService _userService;
        [Required]
        public string UserName { get; set; }
        [Required, PasswordPropertyText]
        public string Password { get; set; }
        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }
        public string LoginErrorMessage { get; set; }
        [Required]
        public string UserNameForPwdReset { get; set; }
        [Required, EmailAddress]
        public string EmailForPwdReset { get; set; }
        public string PwdResetSuccessMessage { get; set; }
        public string PwdResetErrorMessage { get; set; }
        public bool ShowPwdResetOptions { get; set; } = false;

        public LoginModel(ForumDbContext context, Utils utils, CacheService cacheService, UserService userService)
        {
            _context = context;
            _utils = utils;
            _cacheService = cacheService;
            _userService = userService;
        }

        public async Task<IActionResult> OnPost()
        {
            var user = from u in _context.PhpbbUsers.AsNoTracking()
                       let cryptedPass = Crypter.Phpass.Crypt(Password, u.UserPassword)
                       where u.UsernameClean == _utils.CleanString(UserName)
                          && cryptedPass == u.UserPassword
                       select u;

            if (user.Count() != 1)
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), "Numele de utilizator și/sau parola sunt greșite!");
                return Page();
            }
            else if (user.First().UserInactiveReason != UserInactiveReason.NotInactive || user.First().UserInactiveTime != 0)
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), "Utilizatorul nu este activat!");
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
                if (await _cacheService.GetFromCache<bool?>(key) ?? false)
                {
                    await _cacheService.RemoveFromCache(key);
                }

                return Redirect(HttpUtility.UrlDecode(ReturnUrl));
            }
        }

        public async Task<IActionResult> OnPostResetPassword()
        {
            var current = await _context.PhpbbUsers.FirstOrDefaultAsync(x => x.UsernameClean == _utils.CleanString(UserNameForPwdReset) && x.UserEmailHash == _utils.CalculateCrc32Hash(EmailForPwdReset));
            if(current == null)
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), "Adresa de email și/sau numele de utilizator introduse nu sunt corecte.");
                ShowPwdResetOptions = true;
                return Page();
            }

            var resetKey = Guid.NewGuid().ToString("n");
            current.UserNewpasswd = Crypter.Phpass.Crypt(resetKey);
            //await _context.SaveChangesAsync();

            try
            {
                var subject = $"Resetează-ți parola pe \"{Constants.FORUM_NAME}\"";
                var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", Constants.FORUM_NAME),
                    Subject = subject,
                    Body = await _utils.RenderRazorViewToString(
                        "_ResetPasswordPartial",
                        new _ResetPasswordPartialModel
                        {
                            Code = resetKey,
                            UserId = current.UserId,
                            UserName = UserName
                        },
                        PageContext,
                        HttpContext
                    ),
                    IsBodyHtml = true
                };
                emailMessage.To.Add(EmailForPwdReset);
                //await _utils.SendEmail(emailMessage);
            }
            catch
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), "A intervenit o eroare, te rugăm să încerci mai târziu.");
                ShowPwdResetOptions = true;
                return Page();
            }

            PwdResetSuccessMessage = "Am trimis un e-mail, la adresa completată, cu mai multe instrucțiuni pe care trebuie să le urmezi ca să îți poți recupera contul.";

            return Page();
        }
    }
}