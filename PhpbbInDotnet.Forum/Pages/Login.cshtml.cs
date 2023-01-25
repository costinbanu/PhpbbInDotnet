using CryptSharp.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Email;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class LoginModel : BaseModel
    {
        private readonly IForumDbContext _context;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IConfiguration _config;
        private readonly IEncryptionService _encryptionService;
        private readonly IEmailService _emailService;

        [BindProperty, Required]
        public string? UserName { get; set; }

        [BindProperty, Required, PasswordPropertyText]
        public string? Password { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? LoginErrorMessage { get; set; }

        [BindProperty, Required]
        public string? UserNameForPwdReset { get; set; }

        [BindProperty, Required, EmailAddress]
        public string? EmailForPwdReset { get; set; }

        public string? PwdResetSuccessMessage { get; set; }

        public string? PwdResetErrorMessage { get; set; }

        public bool ShowPwdResetOptions { get; set; } = false;

        [BindProperty, Required, PasswordPropertyText]
        public string? PwdResetFirstPassword { get; set; }

        [BindProperty, Required, PasswordPropertyText]
        public string? PwdResetSecondPassword { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ResetPasswordCode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid Init { get; set; }

        public LoginMode Mode { get; private set; }

        public LoginModel(IForumDbContext context, ISqlExecuter sqlExecuter, IConfiguration config, ITranslationProvider translationProvider, 
            IEncryptionService encryptionService, IEmailService emailService)
            : base(translationProvider)
        {
            _context = context;
            _sqlExecuter = sqlExecuter;
            _config = config;
            _encryptionService = encryptionService;
            _emailService = emailService;
        }

        public IActionResult OnGet()
        {
            if (AuthenticatedUserExpanded.TryGetValue(HttpContext, out var user) && !user.IsAnonymous)
            {
                return RedirectToPage("Index");
            }
            Mode = LoginMode.Normal;
            return Page();
        }

        public async Task<IActionResult> OnGetNewPassword()
        {
            if (AuthenticatedUserExpanded.TryGetValue(HttpContext, out var loggedUser) && !loggedUser.IsAnonymous)
            {
                return RedirectToPage("Logout", new { returnUrl = ReturnUrl ?? "/" });
            }

            var user = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);

            if (user == null || ResetPasswordCode != await _encryptionService.DecryptAES(user.UserNewpasswd, Init))
            {
                return PageWithError(nameof(PwdResetErrorMessage), TranslationProvider.Errors[Language, "CONFIRM_ERROR"]);
            }
            Mode = LoginMode.PasswordReset;
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var user = await _sqlExecuter.QueryAsync<PhpbbUsers>(
                "SELECT * FROM phpbb_users WHERE username_clean = @username", 
                new { username = StringUtility.CleanString(UserName) });

            Mode = LoginMode.Normal;
            if (user.Count() != 1)
            {
                return PageWithError(nameof(LoginErrorMessage), TranslationProvider.Errors[Language, "WRONG_USER_PASS"]);
            }

            var currentUser = user.First();
            if ((currentUser.UserInactiveReason != UserInactiveReason.NotInactive && currentUser.UserInactiveReason != UserInactiveReason.Active_NotConfirmed) || currentUser.UserInactiveTime != 0)
            {
                return PageWithError(nameof(LoginErrorMessage), TranslationProvider.Errors[Language, "INACTIVE_USER"]);
            }

            if (currentUser.UserPassword != Crypter.Phpass.Crypt(Password!, currentUser.UserPassword))
            {
                return PageWithError(nameof(LoginErrorMessage), TranslationProvider.Errors[Language, "WRONG_USER_PASS"]);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                IdentityUtility.CreateClaimsPrincipal(currentUser.UserId),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.Add(_config.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30)),
                    IsPersistent = true,
                });

            if (currentUser.UserShouldSignIn)
            {
                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_users SET user_should_sign_in = 0 WHERE user_id = @userId",
                    new { currentUser.UserId });
            }

            var returnUrl = HttpUtility.UrlDecode(ReturnUrl ?? "/");
            if (returnUrl.StartsWith("/login", StringComparison.InvariantCultureIgnoreCase) ||
                returnUrl.StartsWith("/logout", StringComparison.InvariantCultureIgnoreCase) ||
                returnUrl.StartsWith("/confirm", StringComparison.InvariantCultureIgnoreCase))
            {
                returnUrl = "/";
            }
            return Redirect(returnUrl);
        }

        public async Task<IActionResult> OnPostResetPassword()
        {
            try
            {
                var user = _context.PhpbbUsers.FirstOrDefault(
                    x => x.UsernameClean == StringUtility.CleanString(UserNameForPwdReset) &&
                    x.UserEmailHash == HashUtility.ComputeCrc64Hash(EmailForPwdReset!)
                );

                if (user == null)
                {
                    return PageWithError(nameof(PwdResetErrorMessage), TranslationProvider.Errors[Language, "WRONG_EMAIL_USER"], toDoBeforeReturn: () =>
                    {
                        ShowPwdResetOptions = true;
                        Mode = LoginMode.PasswordReset;
                    });
                }

                var resetKey = Guid.NewGuid().ToString("n");
                var (encrypted, iv) = await _encryptionService.EncryptAES(resetKey);
                user.UserNewpasswd = encrypted;

                var dbChangesTask = _context.SaveChangesAsync();

                var emailTask = _emailService.SendEmail(
                    to: EmailForPwdReset!,
                    subject: string.Format(TranslationProvider.Email[Language, "RESETPASS_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName")),
                    bodyRazorViewName: "_ResetPasswordPartial",
                    bodyRazorViewModel: new _ResetPasswordPartialModel(resetKey, user.UserId, user.Username, iv, Language));

                await Task.WhenAll(dbChangesTask, emailTask);
            }
            catch
            {
                return PageWithError(nameof(PwdResetErrorMessage), TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED_TRY_AGAIN"], toDoBeforeReturn: () =>
                {
                    ShowPwdResetOptions = true;
                    Mode = LoginMode.PasswordReset;
                });
            }

            return RedirectToPage("Confirm", "NewPassword");
        }

        public async Task<IActionResult> OnPostSaveNewPassword()
        {
            var validator = new UserProfileDataValidationService(ModelState, TranslationProvider, Language);
            var validations = new[]
            {
                validator.ValidatePassword(nameof(PwdResetErrorMessage), PwdResetFirstPassword),
                validator.ValidateSecondPassword(nameof(PwdResetErrorMessage), PwdResetSecondPassword, PwdResetFirstPassword),
            };

            if (!validations.All(x => x))
            {
                Mode = LoginMode.PasswordReset;
                return Page();
            }

            var user = _context.PhpbbUsers.FirstOrDefault(u => u.UserId == UserId);
            if (user == null || ResetPasswordCode != await _encryptionService.DecryptAES(user.UserNewpasswd, Init))
            {
                return PageWithError(nameof(PwdResetErrorMessage), TranslationProvider.Errors[Language, "CONFIRM_ERROR"], toDoBeforeReturn: () => Mode = LoginMode.PasswordReset);                
            }

            user.UserNewpasswd = string.Empty;
            user.UserPassword = Crypter.Phpass.Crypt(PwdResetFirstPassword!, Crypter.Phpass.GenerateSalt());
            user.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
            await _context.SaveChangesAsync();

            return RedirectToPage("Confirm", "PasswordChanged");
        }
    }
}