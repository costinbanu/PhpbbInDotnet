using CryptSharp.Core;
using Diacritics.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [BindProperties, ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly ForumDbContext _context;
        private readonly Utils _utils;
        private readonly CacheService _cacheService;
        private readonly UserService _userService;
        private readonly IConfiguration _config;

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
        
        [Required, PasswordPropertyText, StringLength(maximumLength: 256, MinimumLength = 8, ErrorMessage = "Parola trebuie să fie de minim 8 caractere lungime")]
        public string PwdResetFirstPassword { get; set; }
        
        [Required, PasswordPropertyText, Compare(otherProperty: nameof(PwdResetFirstPassword), ErrorMessage = "Cele două parole trebuie să fie identice")]
        public string PwdResetSecondPassword { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string ResetPasswordCode { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid Init { get; set; }

        public LoginMode Mode { get; set; }

        public LoginModel(ForumDbContext context, Utils utils, CacheService cacheService, UserService userService, IConfiguration config)
        {
            _context = context;
            _utils = utils;
            _cacheService = cacheService;
            _userService = userService;
            _config = config;
        }

        public async Task<IActionResult> OnGet()
        {
            if ((User?.Identity?.IsAuthenticated ?? false) && !((await _userService.ClaimsPrincipalToLoggedUserAsync(User))?.IsAnonymous ?? true))
            {
                return RedirectToPage("Logout", new { returnUrl = ReturnUrl ?? "/" });
            }
            Mode = LoginMode.Normal;
            return Page();
        }

        public async Task<IActionResult> OnGetNewPassword()
        {
            if ((User?.Identity?.IsAuthenticated ?? false) && !((await _userService.ClaimsPrincipalToLoggedUserAsync(User))?.IsAnonymous ?? true))
            {
                return RedirectToPage("Logout", new { returnUrl = ReturnUrl ?? "/" });
            }

            var user = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);

            if (user == null || ResetPasswordCode != await _utils.DecryptAES(user.UserNewpasswd, Init))
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), "A intervenit o eroare - utilizatorul nu există sau codul de resetare a parolei este greșit.");
                return Page();
            }
            Mode = LoginMode.PasswordReset;
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var user = await _context.PhpbbUsers.AsNoTracking().Where(u => u.UsernameClean == _utils.CleanString(UserName)).ToListAsync();
            user = user.Where(u => Crypter.Phpass.Crypt(Password, u.UserPassword) == u.UserPassword).ToList();

            if (!user.Any() && _config.GetValue<bool>("CompatibilityMode") && "ăîâșțĂÎÂȘȚ".Any(c => UserName.Contains(c)))
            {
                var cache = await _context.PhpbbUsers.AsNoTracking().ToListAsync();
                user = cache.Where(u => _utils.CleanString(u.Username) == _utils.CleanString(UserName) && Crypter.Phpass.Crypt(Password, u.UserPassword) == u.UserPassword).ToList();
            }

            Mode = LoginMode.Normal;
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

                return Redirect(HttpUtility.UrlDecode(ReturnUrl ?? "/"));
            }
        }

        public async Task<IActionResult> OnPostResetPassword()
        {
            var user = await _context.PhpbbUsers.FirstOrDefaultAsync(
                x => x.UsernameClean == _utils.CleanString(UserNameForPwdReset) && 
                x.UserEmailHash == _utils.CalculateCrc32Hash(EmailForPwdReset)
            );

            if(user == null)
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), "Adresa de email și/sau numele de utilizator introduse nu sunt corecte.");
                ShowPwdResetOptions = true;
                Mode = LoginMode.PasswordReset;
                return Page();
            }

            var resetKey = Guid.NewGuid().ToString("n");
            var (encrypted, iv) = await _utils.EncryptAES(resetKey);
            user.UserNewpasswd = encrypted;
            await _context.SaveChangesAsync();

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
                            IV = iv,
                            UserId = user.UserId,
                            UserName = UserName
                        },
                        PageContext,
                        HttpContext
                    ),
                    IsBodyHtml = true
                };
                emailMessage.To.Add(EmailForPwdReset);
                await _utils.SendEmail(emailMessage);
            }
            catch
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), "A intervenit o eroare, te rugăm să încerci mai târziu.");
                ShowPwdResetOptions = true;
                Mode = LoginMode.PasswordReset;
                return Page();
            }

            return RedirectToPage("Confirm", "NewPassword");
        }

        public async Task<IActionResult> OnPostSaveNewPassword()
        {
            var user = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == UserId);
            if (user == null || ResetPasswordCode != await _utils.DecryptAES(user.UserNewpasswd, Init))
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), "A intervenit o eroare - utilizatorul nu există sau codul de resetare a parolei este greșit.");
                Mode = LoginMode.PasswordReset;
                return Page();
            }

            user.UserNewpasswd = string.Empty;
            user.UserPassword = Crypter.Phpass.Crypt(PwdResetFirstPassword, Crypter.Phpass.GenerateSalt());
            user.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
            await _context.SaveChangesAsync();

            return RedirectToPage("Confirm", "PasswordChanged");
        }
    }
}