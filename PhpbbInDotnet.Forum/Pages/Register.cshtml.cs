using CryptSharp.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;
using Serilog;
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
        private readonly IConfiguration _config;
        private readonly HttpClient _gClient;
        private readonly Recaptcha _recaptchaOptions;
        private readonly IUserService _userService;
        private readonly ILogger _logger;
        private readonly IEmailService _emailService;

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

        public ITranslationProvider TranslationProvider { get; }


        public RegisterModel(IForumDbContext context, IConfiguration config, IHttpClientFactory httpClientFactory, ITranslationProvider translationProvider,
            IUserService userService, ILogger logger, IEmailService emailService)
        {
            _context = context;
            _config = config;
            _recaptchaOptions = _config.GetObject<Recaptcha>();
            _gClient = httpClientFactory.CreateClient(_recaptchaOptions.ClientName);
            TranslationProvider = translationProvider;
            _userService = userService;
            _logger = logger;
            _emailService = emailService;
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
            var lang = TranslationProvider.GetLanguage();
            var validator = new UserProfileDataValidationService(ModelState, TranslationProvider, lang);
            Email = Email?.Trim();
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
                    return PageWithError(nameof(RecaptchaResponse), string.Format(TranslationProvider.Errors[lang, "YOURE_A_BOT_FORMAT"], _config.GetValue<string>("AdminEmail").Replace("@", " at ").Replace(".", " dot ")));
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to check captcha");
                return PageWithError(nameof(RecaptchaResponse), TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
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
                return PageWithError(nameof(Email), TranslationProvider.Errors[lang, "BANNED_EMAIL"]);
            }

            if (checkBanlist.Any(x => (long)x.IP == 1L))
            {
                return PageWithError(nameof(UserName), TranslationProvider.Errors[lang, "BANNED_IP"]);
            }

            if (await _context.PhpbbUsers.AsNoTracking().AnyAsync(u => u.UsernameClean == StringUtility.CleanString(UserName)))
            {
                return PageWithError(nameof(UserName), TranslationProvider.Errors[lang, "EXISTING_USERNAME"]);
            }

            if (await _context.PhpbbUsers.AsNoTracking().AnyAsync(u => u.UserEmailHash == HashUtility.ComputeCrc64Hash(Email)))
            {
                return PageWithError(nameof(Email), TranslationProvider.Errors[lang, "EXISTING_EMAIL"]);
            }

            var registrationCode = Guid.NewGuid().ToString("n");
            var now = DateTime.UtcNow.ToUnixTimestamp();

            var newUser = _context.PhpbbUsers.Add(new PhpbbUsers
            {
                Username = UserName!,
                UsernameClean = StringUtility.CleanString(UserName),
                GroupId = 2,
                UserEmail = Email,
                UserEmailHash = HashUtility.ComputeCrc64Hash(Email),
                UserPassword = Crypter.Phpass.Crypt(Password!, Crypter.Phpass.GenerateSalt()),
                UserInactiveTime = now,
                UserInactiveReason = UserInactiveReason.NewlyRegisteredNotConfirmed,
                UserActkey = registrationCode,
                UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserRegdate = now,
                UserLastmark = now,
                UserDateformat = TranslationProvider.GetDefaultDateFormat(lang),
                UserLang = lang
            });
            newUser.Entity.UserId = 0;
            await _context.SaveChangesAsync();

            _context.PhpbbUserGroup.Add(new PhpbbUserGroup
            {
                GroupId = 2,
                UserId = newUser.Entity.UserId
            });
            var subject = string.Format(TranslationProvider.Email[lang, "WELCOME_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName"));

            var dbChangesTask = _context.SaveChangesAsync();
            var emailTask = _emailService.SendEmail(
                to: Email,
                subject: subject,
                bodyRazorViewName: "_WelcomeEmailPartial",
                bodyRazorViewModel: new WelcomeEmailDto(subject, registrationCode, UserName!, lang));
            await Task.WhenAll(dbChangesTask, emailTask);

            return RedirectToPage("Confirm", "RegistrationComplete");
        }

        private IActionResult PageWithError(string errorKey, string errorMessage)
        {
            ModelState.AddModelError(errorKey, errorMessage);
            return Page();
        }
    }
}