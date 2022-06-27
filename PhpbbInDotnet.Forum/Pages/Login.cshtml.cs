using CryptSharp.Core;
using LazyCache;
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
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using PhpbbInDotnet.Utilities.Core;
using PhpbbInDotnet.Utilities.Extensions;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly IForumDbContext _context;
        private readonly ICommonUtils _utils;
        private readonly IAppCache _cache;
        private readonly IUserService _userService;
        private readonly IConfiguration _config;

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
        public LanguageProvider LanguageProvider { get; }

        public LoginModel(IForumDbContext context, ICommonUtils utils, IAppCache cache, IUserService userService, IConfiguration config, LanguageProvider languageProvider)
        {
            _context = context;
            _utils = utils;
            _cache = cache;
            _userService = userService;
            _config = config;
            LanguageProvider = languageProvider;
        }

        public IActionResult OnGet()
        {
            var currentUser = _userService.ClaimsPrincipalToAuthenticatedUser(User);
            if (currentUser?.IsAnonymous == false)
            {
                return RedirectToPage("Index");
            }
            Mode = LoginMode.Normal;
            return Page();
        }

        public async Task<IActionResult> OnGetNewPassword()
        {
            if (User?.Identity?.IsAuthenticated == true && _userService.ClaimsPrincipalToAuthenticatedUser(User)?.IsAnonymous != true)
            {
                return RedirectToPage("Logout", new { returnUrl = ReturnUrl ?? "/" });
            }

            var user = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);

            if (user == null || ResetPasswordCode != await _utils.DecryptAES(user.UserNewpasswd, Init))
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), LanguageProvider.Errors[LanguageProvider.GetValidatedLanguage(null, Request), "CONFIRM_ERROR"]);
                return Page();
            }
            Mode = LoginMode.PasswordReset;
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var sqlExecuter = _context.GetSqlExecuter();

            var user = await sqlExecuter.QueryAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE username_clean = @username", new { username = StringUtility.CleanString(UserName) });
            var lang = LanguageProvider.GetValidatedLanguage(null, Request);

            Mode = LoginMode.Normal;
            if (user.Count() != 1)
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), LanguageProvider.Errors[lang, "WRONG_USER_PASS"]);
                return Page();
            }

            var currentUser = user.First();
            if (currentUser.UserInactiveReason != UserInactiveReason.NotInactive || currentUser.UserInactiveTime != 0)
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), LanguageProvider.Errors[lang, "INACTIVE_USER"]);
                return Page();
            }

            if (currentUser.UserPassword != Crypter.Phpass.Crypt(Password!, currentUser.UserPassword))
            {
                ModelState.AddModelError(nameof(LoginErrorMessage), LanguageProvider.Errors[lang, "WRONG_USER_PASS"]);
                return Page();
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                await _userService.DbUserToClaimsPrincipal(currentUser),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.Add(_config.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30)),
                    IsPersistent = true,
                });

            var key = $"UserMustLogIn_{currentUser.UsernameClean}";
            if (await _cache.GetAsync<bool?>(key) ?? false)
            {
                _cache.Remove(key);
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
            var lang = LanguageProvider.GetValidatedLanguage(null, Request);
            try
            {
                var user = _context.PhpbbUsers.FirstOrDefault(
                    x => x.UsernameClean == StringUtility.CleanString(UserNameForPwdReset) &&
                    x.UserEmailHash == HashingUtility.ComputeCrc64Hash(EmailForPwdReset!)
                );

                if (user == null)
                {
                    ModelState.AddModelError(nameof(PwdResetErrorMessage), LanguageProvider.Errors[lang, "WRONG_EMAIL_USER"]);
                    ShowPwdResetOptions = true;
                    Mode = LoginMode.PasswordReset;
                    return Page();
                }

                var resetKey = Guid.NewGuid().ToString("n");
                var (encrypted, iv) = await _utils.EncryptAES(resetKey);
                user.UserNewpasswd = encrypted;

                var dbChangesTask = _context.SaveChangesAsync();

                var emailTask = _utils.SendEmail(
                    to: EmailForPwdReset!,
                    subject: string.Format(LanguageProvider.Email[lang, "RESETPASS_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName")),
                    body: await _utils.RenderRazorViewToString(
                        "_ResetPasswordPartial",
                        new _ResetPasswordPartialModel
                        {
                            Code = resetKey,
                            IV = iv,
                            UserId = user.UserId,
                            UserName = user.Username
                        },
                        PageContext,
                        HttpContext
                    ));

                await Task.WhenAll(dbChangesTask, emailTask);
            }
            catch
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                ShowPwdResetOptions = true;
                Mode = LoginMode.PasswordReset;
                return Page();
            }

            return RedirectToPage("Confirm", "NewPassword");
        }

        public async Task<IActionResult> OnPostSaveNewPassword()
        {
            var lang = LanguageProvider.GetValidatedLanguage(null, Request);
            var validator = new UserProfileDataValidationService(ModelState, LanguageProvider, lang);
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
            if (user == null || ResetPasswordCode != await _utils.DecryptAES(user.UserNewpasswd, Init))
            {
                ModelState.AddModelError(nameof(PwdResetErrorMessage), LanguageProvider.Errors[lang, "CONFIRM_ERROR"]);
                Mode = LoginMode.PasswordReset;
                return Page();
            }

            user.UserNewpasswd = string.Empty;
            user.UserPassword = Crypter.Phpass.Crypt(PwdResetFirstPassword!, Crypter.Phpass.GenerateSalt());
            user.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
            await _context.SaveChangesAsync();

            return RedirectToPage("Confirm", "PasswordChanged");
        }
    }
}