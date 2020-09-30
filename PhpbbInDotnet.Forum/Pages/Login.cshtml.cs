using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Email;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [BindProperties, ValidateAntiForgeryToken, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class LoginModel : PageModel
    {
        private readonly ForumDbContext _context;
        private readonly CommonUtils _utils;
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

        public LoginModel(ForumDbContext context, CommonUtils utils, CacheService cacheService, UserService userService, IConfiguration config)
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
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();

            var user = await connection.QueryAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE CONVERT(LOWER(username) USING utf8) = @username", new { username = _utils.CleanString(UserName) });

            Mode = LoginMode.Normal;
            if (user.Count() != 1)
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), "Numele de utilizator și/sau parola sunt greșite!");
                return Page();
            }

            var currentUser = user.First();
            if (currentUser.UserInactiveReason != UserInactiveReason.NotInactive || currentUser.UserInactiveTime != 0)
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), "Utilizatorul nu este activat!");
                return Page();
            }

            if (currentUser.UserPassword != Crypter.Phpass.Crypt(Password, currentUser.UserPassword))
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), "Numele de utilizator și/sau parola sunt greșite!");
                return Page();
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                await _userService.DbUserToClaimsPrincipalAsync(currentUser),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.Add(TimeSpan.FromDays(_config.GetValue<int>("LoginSessionSlidingExpirationDays"))),
                    IsPersistent = true,
                });

            var key = $"UserMustLogIn_{currentUser.UsernameClean}";
            if (await _cacheService.GetFromCache<bool?>(key) ?? false)
            {
                await _cacheService.RemoveFromCache(key);
            }

            return Redirect(HttpUtility.UrlDecode(ReturnUrl ?? "/"));
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
                var subject = $"Resetează-ți parola pe \"{_config.GetValue<string>("ForumName")}\"";
                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
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