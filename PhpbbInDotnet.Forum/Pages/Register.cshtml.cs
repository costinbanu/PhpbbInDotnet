using CryptSharp.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Domain.Extensions;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        private readonly IForumDbContext _context;
        private readonly ICommonUtils _utils;
        private readonly IConfiguration _config;
        private readonly HttpClient _gClient;
        private readonly Recaptcha _recaptchaOptions;
        private readonly IUserService _userService;

        private string? _language;

        [BindProperty]
        public string? Email { get; set; }

        [BindProperty]
        public string? UserName { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        [BindProperty]
        public string? SecondPassword { get; set; }

        [BindProperty]
        public bool Agree { get; set; }

        [BindProperty]
        public string? RecaptchaResponse { get; set; }

        public LanguageProvider LanguageProvider { get; }


        public RegisterModel(IForumDbContext context, ICommonUtils utils, IConfiguration config, IHttpClientFactory httpClientFactory, LanguageProvider languageProvider, IUserService userService)
        {
            _context = context;
            _utils = utils;
            _config = config;
            _recaptchaOptions = _config.GetObject<Recaptcha>();
            _gClient = httpClientFactory.CreateClient(_recaptchaOptions.ClientName);
            LanguageProvider = languageProvider;
            _userService = userService;
        }

        public IActionResult OnGet()
        {
            var currentUser = _userService.ClaimsPrincipalToAuthenticatedUser(User);
            if (currentUser?.IsAnonymous == false)
            {
                return RedirectToPage("Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var lang = GetLanguage();
            var validator = new UserProfileDataValidationService(ModelState, LanguageProvider, lang);
            var validations = new[]
            {
                validator.ValidateUsername(nameof(UserName), UserName),
                validator.ValidateEmail(nameof(Email), Email),
                validator.ValidatePassword(nameof(Password), Password),
                validator.ValidateSecondPassword(nameof(SecondPassword), SecondPassword, Password),
                validator.ValidateTermsAgreement(nameof(Agree), Agree)
            };

            if (!validations.All(x => x))
            {
                return Page();
            }

            try
            {
                var response = await _gClient.PostAsync(
                    requestUri: _recaptchaOptions.RelativeUri,
                    content: new StringContent(
                        content: $"secret={_recaptchaOptions.SecretKey}&response={RecaptchaResponse}&remoteip={HttpContext.Connection.RemoteIpAddress}",
                        encoding: Encoding.UTF8,
                        mediaType: "application/x-www-form-urlencoded"
                    )
                );
                response.EnsureSuccessStatusCode();
                var resultText = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(resultText);
                if ((bool?)result?.success != true)
                {
                    throw new InvalidOperationException($"Validating g-recaptcha failed. Response: {resultText}");
                }
                if ((decimal)result.score < _recaptchaOptions.MinScore)
                {
                    return PageWithError(nameof(RecaptchaResponse), string.Format(LanguageProvider.Errors[lang, "YOURE_A_BOT_FORMAT"], _config.GetValue<string>("AdminEmail").Replace("@", " at ").Replace(".", " dot ")));
                }
            }
            catch (Exception ex)
            {
                _utils.HandleErrorAsWarning(ex, "Failed to check captcha");
                return PageWithError(nameof(RecaptchaResponse), LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
            }

            var sqlExecuter = _context.GetSqlExecuter();

            var checkBanlist = await sqlExecuter.QueryAsync(
                @"SELECT @email LIKE LOWER(REPLACE(REPLACE(ban_email, '*', '%'), '?', '_')) AS Email,
                         @ip LIKE LOWER(REPLACE(REPLACE(ban_ip, '*', '%'), '?', '_')) AS IP
                    FROM phpbb_banlist
                   WHERE @email LIKE LOWER(REPLACE(REPLACE(ban_email, '*', '%'), '?', '_')) 
                      OR @ip LIKE LOWER(REPLACE(REPLACE(ban_ip, '*', '%'), '?', '_'))",
                new { email = Email!.ToLower(), ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty }
            );

            if (checkBanlist.Any(x => (long)x.Email == 1L))
            {
                return PageWithError(nameof(Email), LanguageProvider.Errors[lang, "BANNED_EMAIL"]);
            }

            if (checkBanlist.Any(x => (long)x.IP == 1L))
            {
                return PageWithError(nameof(UserName), LanguageProvider.Errors[lang, "BANNED_IP"]);
            }

            if (await _context.PhpbbUsers.AsNoTracking().AnyAsync(u => u.UsernameClean == StringUtility.CleanString(UserName)))
            {
                return PageWithError(nameof(UserName), LanguageProvider.Errors[lang, "EXISTING_USERNAME"]);
            }

            if (await _context.PhpbbUsers.AsNoTracking().AnyAsync(u => u.UserEmailHash == HashingUtility.ComputeCrc64Hash(Email)))
            {
                return PageWithError(nameof(Email), LanguageProvider.Errors[lang, "EXISTING_EMAIL"]);
            }

            var registrationCode = Guid.NewGuid().ToString("n");
            var now = DateTime.UtcNow.ToUnixTimestamp();

            var newUser = _context.PhpbbUsers.Add(new PhpbbUsers
            {
                Username = UserName!,
                UsernameClean = StringUtility.CleanString(UserName),
                GroupId = 2,
                UserEmail = Email,
                UserEmailHash = HashingUtility.ComputeCrc64Hash(Email),
                UserPassword = Crypter.Phpass.Crypt(Password!, Crypter.Phpass.GenerateSalt()),
                UserInactiveTime = now,
                UserInactiveReason = UserInactiveReason.NewlyRegisteredNotConfirmed,
                UserActkey = registrationCode,
                UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserRegdate = now,
                UserLastmark = now,
                UserDateformat = LanguageProvider.GetDefaultDateFormat(lang),
                UserLang = lang
            });
            newUser.Entity.UserId = 0;
            await _context.SaveChangesAsync();

            _context.PhpbbUserGroup.Add(new PhpbbUserGroup
            {
                GroupId = 2,
                UserId = newUser.Entity.UserId
            });
            var subject = string.Format(LanguageProvider.Email[LanguageProvider.GetValidatedLanguage(null, Request), "WELCOME_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName"));

            var dbChangesTask = _context.SaveChangesAsync();
            var emailTask = _utils.SendEmail(
                to: Email,
                subject: subject,
                body: await _utils.RenderRazorViewToString(
                    "_WelcomeEmailPartial",
                    new WelcomeEmailDto
                    {
                        RegistrationCode = registrationCode,
                        Subject = subject,
                        UserName = UserName
                    },
                    PageContext,
                    HttpContext
                ));
            await Task.WhenAll(dbChangesTask, emailTask);

            return RedirectToPage("Confirm", "RegistrationComplete");
        }

        private IActionResult PageWithError(string errorKey, string errorMessage)
        {
            ModelState.AddModelError(errorKey, errorMessage);
            return Page();
        }

        public string GetLanguage() => _language ??= LanguageProvider.GetValidatedLanguage(null, HttpContext?.Request);
    }
}