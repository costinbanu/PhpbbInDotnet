using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Email;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        private readonly ForumDbContext _context;
        private readonly CommonUtils _utils;
        private readonly IConfiguration _config;
        private readonly HttpClient _gClient;
        
        private string _language;

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string UserName { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string SecondPassword { get; set; }

        [BindProperty]
        public bool Agree { get; set; }

        [BindProperty]
        public string RecaptchaResponse { get; set; }

        public LanguageProvider LanguageProvider { get; }

        public RegisterModel(ForumDbContext context, CommonUtils utils, IConfiguration config, IHttpClientFactory httpClientFactory, LanguageProvider languageProvider)
        {
            _context = context;
            _utils = utils;
            _config = config;
            _gClient = httpClientFactory.CreateClient(config["Recaptcha:ClientName"]);
            LanguageProvider = languageProvider;
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
                    requestUri: _config.GetValue<string>("Recaptcha:RelativeUri"),
                    content: new StringContent(
                        content: $"secret={_config.GetValue<string>("Recaptcha:SecretKey")}&response={RecaptchaResponse}&remoteip={HttpContext.Connection.RemoteIpAddress}",
                        encoding: Encoding.UTF8,
                        mediaType: "application/x-www-form-urlencoded"
                    )
                );
                response.EnsureSuccessStatusCode();
                var resultText = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(resultText);
                if (!(bool)result.success)
                {
                    throw new InvalidOperationException($"Validating g-recaptcha failed. Response: {resultText}");
                }
                if ((decimal)result.score < _config.GetValue<decimal>("Recaptcha:Score"))
                {
                    return PageWithError(nameof(RecaptchaResponse), string.Format(LanguageProvider.Errors[lang, "YOURE_A_BOT_FORMAT"], _config.GetValue<string>("AdminEmail").Replace("@", " at ").Replace(".", " dot ")));
                }
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, "Failed to check captcha");
                return PageWithError(nameof(RecaptchaResponse), LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
            }

            var conn = _context.Database.GetDbConnection();

            var checkBanlist = await conn.QueryAsync(
                @"SELECT @email LIKE LOWER(REPLACE(REPLACE(ban_email, '*', '%'), '?', '_')) AS Email,
                         @ip LIKE LOWER(REPLACE(REPLACE(ban_ip, '*', '%'), '?', '_')) AS IP
                    FROM phpbb_banlist
                   WHERE @email LIKE LOWER(REPLACE(REPLACE(ban_email, '*', '%'), '?', '_')) 
                      OR @ip LIKE LOWER(REPLACE(REPLACE(ban_ip, '*', '%'), '?', '_'))",
                new { email = Email.ToLower(), ip = HttpContext.Connection.RemoteIpAddress.ToString() }
            );

            if (checkBanlist.Any(x => x.Email == 1L))
            {
                return PageWithError(nameof(Email), LanguageProvider.Errors[lang, "BANNED_EMAIL"]);
            }

            if (checkBanlist.Any(x => x.IP == 1L))
            {
                return PageWithError(nameof(UserName), LanguageProvider.Errors[lang, "BANNED_IP"]);
            }

            var checkUsers = await conn.QueryAsync(
                @"SELECT username_clean = @usernameClean as Username, user_email_hash = @emailHash AS Email
                    FROM phpbb_users 
                   WHERE username_clean = @usernameClean 
                      OR user_email_hash = @emailHash",
                new { usernameClean = _utils.CleanString(UserName), emailHash = _utils.CalculateCrc32Hash(Email) }
            );

            if (checkUsers.Any(u => u.Username == 1L))
            {
                return PageWithError(nameof(UserName), LanguageProvider.Errors[lang, "EXISTING_USERNAME"]);
            }

            if (checkUsers.Any(u => u.Email == 1L))
            {
                return PageWithError(nameof(Email), LanguageProvider.Errors[lang, "EXISTING_EMAIL"]);
            }

            var registrationCode = Guid.NewGuid().ToString("n");
            var now = DateTime.UtcNow.ToUnixTimestamp();

            await conn.ExecuteAsync(
                @"INSERT INTO phpbb_users (group_id, user_permissions, username, username_clean, user_email, user_email_hash, user_password, user_inactive_time, user_inactive_reason, user_actkey, user_ip, user_regdate, user_lastmark, user_sig, user_occ, user_interests)
                    VALUES (2, '', @UserName, @UsernameClean, @UserEmail, @UserEmailHash, @UserPassword, @UserInactiveTime, @UserInactiveReason, @UserActkey, @UserIp, @UserRegdate, @UserLastmark, '', '', ''); 
                  INSERT INTO phpbb_user_group (group_id, user_id)
                    VALUES (2, LAST_INSERT_ID());",
                new
                {
                    UserName,
                    UsernameClean = _utils.CleanString(UserName),
                    UserEmail = Email,
                    UserEmailHash = _utils.CalculateCrc32Hash(Email),
                    UserPassword = Crypter.Phpass.Crypt(Password, Crypter.Phpass.GenerateSalt()),
                    UserInactiveTime = now,
                    UserInactiveReason = UserInactiveReason.NewlyRegisteredNotConfirmed,
                    UserActkey = registrationCode,
                    UserIp = HttpContext.Connection.RemoteIpAddress.ToString(),
                    UserRegdate = now,
                    UserLastmark = now
                }
            );

            var subject = string.Format(LanguageProvider.Email[LanguageProvider.GetValidatedLanguage(null, Request), "WELCOME_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName"));
            using var emailMessage = new MailMessage
            {
                From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
                Subject = subject,
                Body = await _utils.RenderRazorViewToString(
                    "_WelcomeEmailPartial",
                    new _WelcomeEmailPartialModel
                    {
                        RegistrationCode = registrationCode,
                        Subject = subject,
                        UserName = UserName
                    },
                    PageContext,
                    HttpContext
                ),
                IsBodyHtml = true
            };
            emailMessage.To.Add(Email);
            await _utils.SendEmail(emailMessage);

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