using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Email;
using PhpbbInDotnet.Utilities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [BindProperties, ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        private readonly ForumDbContext _context;
        private readonly CommonUtils _utils;
        private readonly IConfiguration _config;
        private readonly HttpClient _gClient;

        [BindProperty]
        [EmailAddress]
        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        public string Email { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        [StringLength(maximumLength: 32, MinimumLength = 2, ErrorMessage = "Numele de utilizator trebuie să aibă o lungime cuprinsă între 2 și 32 de caractere.")]
        [RegularExpression(@"[a-zA-Z0-9 \._-]+", ErrorMessage = "Caractere permise în numele de utilizator: a-z, A-Z, 0-9, [space], [dot], [underscore], [dash].")]
        public string UserName { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$", ErrorMessage = "Parola trebuie să conțină cel puțin o literă și o cifră")]
        [StringLength(maximumLength: 256, MinimumLength = 8, ErrorMessage = "Parola trebuie să fie de minim 8 caractere lungime")]
        public string Password { get; set; }

        [BindProperty]
        public string SecondPassword { get; set; }

        [BindProperty]
        [Required]
        [Range(type: typeof(bool), minimum: "True", maximum: "True", ErrorMessage = "Trebuie să fii de acord cu termenele de utilizare.")]
        public bool Agree { get; set; }

        [BindProperty]
        public string RecaptchaResponse { get; set; }

        public RegisterModel(ForumDbContext context, CommonUtils utils, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _utils = utils;
            _config = config;
            _gClient = httpClientFactory.CreateClient(config["Recaptcha:ClientName"]);
        }

        public async Task<IActionResult> OnPost()
        {
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
                    return PageWithError(nameof(RecaptchaResponse), "Procesul de înregistrare nu poate continua - sistemul nostru a detectat că ești bot. Dacă este o eroare, scrie-ne la admin arond metrouusor punct com.");
                }
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, "Failed to check captcha");
                return PageWithError(nameof(RecaptchaResponse), "A intervenit o eroare. Te rugăm să încerci mai târziu sau să ne contactezi la admin arond metrouusor punct com.");
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
                return PageWithError(nameof(Email), "Adresa de e-mail nu este acceptată. Te rugăm să încerci cu alta.");
            }

            if (checkBanlist.Any(x => x.IP == 1L))
            {
                return PageWithError(nameof(UserName), "Adresa IP nu este permisă la înregistrare");
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
                return PageWithError(nameof(UserName), "Există deja un utilizator înregistrat cu acest nume de utilizator!");
            }

            if (checkUsers.Any(u => u.Email == 1L))
            {
                return PageWithError(nameof(Email), "Există deja un utilizator înregistrat cu această adresă de email!");
            }

            //TODO: Revert to CompareAttribute in .net 5. Ref: https://github.com/dotnet/aspnetcore/issues/4895
            if (SecondPassword != Password)
            {
                return PageWithError(nameof(SecondPassword), "Cele două parole trebuie să fie identice!");
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

            var subject = $"Bine ai venit la \"{_config.GetValue<string>("ForumName")}\"";
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
    }
}