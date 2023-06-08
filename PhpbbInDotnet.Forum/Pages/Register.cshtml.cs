﻿using CryptSharp.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
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
    public class RegisterModel : BaseModel
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly HttpClient _gClient;
        private readonly Recaptcha _recaptchaOptions;
        private readonly ILogger _logger;
        private readonly IEmailService _emailService;
        private readonly IUserProfileDataValidationService _validationService;

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

        public RegisterModel(ISqlExecuter sqlExecuter, IConfiguration config, IHttpClientFactory httpClientFactory, ITranslationProvider translationProvider, 
            IUserService userService, ILogger logger, IEmailService emailService, IUserProfileDataValidationService validationService)
            : base(translationProvider, userService, config)
        {
            _sqlExecuter = sqlExecuter;
            _recaptchaOptions = Configuration.GetObject<Recaptcha>();
            _gClient = httpClientFactory.CreateClient(_recaptchaOptions.ClientName);
            _logger = logger;
            _emailService = emailService;
            _validationService = validationService;
        }

        public IActionResult OnGet()
        {
            if (!ForumUser.IsAnonymous)
            {
                return RedirectToPage("Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            ThrowIfEntireForumIsReadOnly();

            Email = Email?.Trim();
            var validations = new[]
            {
                _validationService.ValidateUsername(nameof(UserName), UserName),
                _validationService.ValidateEmail(nameof(Email), Email),
                _validationService.ValidatePassword(nameof(Password), Password),
                _validationService.ValidateSecondPassword(nameof(SecondPassword), SecondPassword, Password),
                _validationService.ValidateTermsAgreement(nameof(Agree), Agree)
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
                    return PageWithError(nameof(RecaptchaResponse), string.Format(TranslationProvider.Errors[Language, "YOURE_A_BOT_FORMAT"], Configuration.GetValue<string>("AdminEmail").Replace("@", " at ").Replace(".", " dot ")));
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to check captcha");
                return PageWithError(nameof(RecaptchaResponse), TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
            }

            var checkBanlist = await _sqlExecuter.QueryAsync(
                @"SELECT @email LIKE LOWER(REPLACE(REPLACE(ban_email, '*', '%'), '?', '_')) AS Email,
                         @ip LIKE LOWER(REPLACE(REPLACE(ban_ip, '*', '%'), '?', '_')) AS IP
                    FROM phpbb_banlist
                   WHERE @email LIKE LOWER(REPLACE(REPLACE(ban_email, '*', '%'), '?', '_')) 
                      OR @ip LIKE LOWER(REPLACE(REPLACE(ban_ip, '*', '%'), '?', '_'))",
                new { email = Email!.ToLower(), ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty }
            );

            if (checkBanlist.Any(x => (long)x.Email == 1L))
            {
                return PageWithError(nameof(Email), TranslationProvider.Errors[Language, "BANNED_EMAIL"]);
            }

            if (checkBanlist.Any(x => (long)x.IP == 1L))
            {
                return PageWithError(nameof(UserName), TranslationProvider.Errors[Language, "BANNED_IP"]);
            }
            var usersWithSameUsername = await _sqlExecuter.ExecuteScalarAsync<int>(
                "SELECT count(1) FROM phpbb_users WHERE username_clean = @usernameClean",
                new { usernameClean = StringUtility.CleanString(UserName) });
            if (usersWithSameUsername > 0)
            {
                return PageWithError(nameof(UserName), TranslationProvider.Errors[Language, "EXISTING_USERNAME"]);
            }

            var usersWithSameEmail = await _sqlExecuter.ExecuteScalarAsync<int>(
                "SELECT count(1) FROM phpbb_users WHERE user_email_hash = @emailHash",
                new { emailHash = HashUtility.ComputeCrc64Hash(Email) });
            if (usersWithSameEmail > 0)
            {
                return PageWithError(nameof(Email), TranslationProvider.Errors[Language, "EXISTING_EMAIL"]);
            }

            var registrationCode = Guid.NewGuid().ToString("n");
            var now = DateTime.UtcNow.ToUnixTimestamp();

            var newUser = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                @$"INSERT INTO phpbb_users(username, username_clean, group_id, user_email, user_email_hash, user_password, user_inactive_time, user_inactive_reason, user_actkey, user_ip, user_regdate, user_lastmark, user_dateformat, user_lang)
                   VALUES (@Username, @UsernameClean, @GroupId, @UserEmail, @UserEmailHash, @UserPassword, @UserInactiveTime, @UserInactiveReason, @UserActkey, @UserIp, @UserRegdate, @UserLastmark, @UserDateformat, @UserLang);
                   SELECT * FROM phpbb_users WHERE user_id = {_sqlExecuter.LastInsertedItemId}",
                new 
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
                    UserDateformat = TranslationProvider.GetDefaultDateFormat(Language),
                    UserLang = Language
                });

            var dbChangesTask = _sqlExecuter.ExecuteAsync(
                "INSERT INTO phpbb_user_group(group_id, user_id) VALUES(2, @userId)", 
                new { newUser.UserId });

            var subject = string.Format(TranslationProvider.Email[Language, "WELCOME_SUBJECT_FORMAT"], Configuration.GetValue<string>("ForumName"));

            var emailTask = _emailService.SendEmail(
                to: Email,
                subject: subject,
                bodyRazorViewName: "_WelcomeEmailPartial",
                bodyRazorViewModel: new WelcomeEmailDto(subject, registrationCode, UserName!, Language));
            await Task.WhenAll(dbChangesTask, emailTask);

            return RedirectToPage("Confirm", "RegistrationComplete");
        }
    }
}