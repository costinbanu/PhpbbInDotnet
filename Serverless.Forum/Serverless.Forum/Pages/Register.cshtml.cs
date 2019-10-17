using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using CryptSharp.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaulMiami.AspNetCore.Mvc.Recaptcha;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages
{
    [ValidateRecaptcha]
    [BindProperties]
    [ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _config;

        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        [StringLength(maximumLength: 32, MinimumLength = 2, ErrorMessage = "Numele de utilizator trebuie să aibă o lungime cuprinsă între 2 și 32 de caractere.")]
        [RegularExpression(@"[a-zA-Z0-9 \._-]+", ErrorMessage = "Caractere permise în numele de utilizator: a-z, A-Z, 0-9, [space], [dot], [underrscore], [dash].")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        [StringLength(maximumLength: 256, MinimumLength = 8, ErrorMessage = "Parola trebuie să fie de minim 8 caractere lungime")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Acest câmp este obligatoriu.")]
        [Compare(otherProperty: nameof(Password), ErrorMessage = "Cele două parole trebuie să fie identice")]
        public string SecondPassword { get; set; }

        [Required]
        [Range(type: typeof(bool), minimum: "True", maximum: "True", ErrorMessage = "Trebuie să fii de acord cu termenele de utilizare.")]
        public bool Agree { get; set; }

        public string ErrorMessage { get; set; }

        public RegisterModel(IHttpClientFactory factory, IConfiguration config)
        {
            _factory = factory;
            _config = config;
        }

        public async Task<IActionResult> OnPost()
        {
            using (var context = new forumContext(_config))
            {
                var check = await (from u in context.PhpbbUsers
                                   where u.UsernameClean == Utils.Instance.CleanString(UserName)
                                      || u.UserEmail == Email
                                   select new { u.UsernameClean, u.UserEmail })
                                  .ToListAsync();

                if (check.Any(u => u.UsernameClean == Utils.Instance.CleanString(UserName)))
                {
                    ErrorMessage = "Există deja un utilizator înregistrat cu acest nume de utilizator!";
                    SecondPassword = Password;
                    return Page();
                }

                if(check.Any(u => u.UserEmail == Email))
                {
                    ErrorMessage = "Există deja un utilizator înregistrat cu această adresă de email!";
                    return Page();
                }

                var registrationCode = Guid.NewGuid().ToString("n");

                var userInsertResult = await context.PhpbbUsers.AddAsync(new PhpbbUsers
                {
                    Username = UserName,
                    UsernameClean = UserName.ToLower(),
                    UserEmail = Email,
                    UserPassword = Crypter.Phpass.Crypt(Password, Crypter.Phpass.GenerateSalt()),
                    UserInactiveTime = DateTime.UtcNow.LocalTimeToTimestamp(),
                    UserActkey = registrationCode
                });

                await context.PhpbbUserGroup.AddAsync(new PhpbbUserGroup
                {
                    GroupId = 2,
                    UserId = userInsertResult.Entity.UserId,
                });

                using (var smtp = new SmtpClient("Your SMTP server address"))
                {
                    var emailMessage = new MailMessage();
                    emailMessage.From = new MailAddress($"admin@metrouusor.com");
                    emailMessage.To.Add(Email);
                    emailMessage.Subject = $"Bine ai venit la \"{Constants.FORUM_NAME}\"";
                    emailMessage.Body =
                        $"<h2>{emailMessage.Subject}</h2><br/><br/>" +
                        "Pentru a continua, trebuie să îți confirmi adresa de email.<br/><br/>" +
                        $"<a href=\"{Constants.FORUM_BASE_URL}/Register?code={registrationCode}\">Apasă aici</a> pentru a o confirma.<br/><br/>" +
                        "O zi bună!";

                    await smtp.SendMailAsync(emailMessage);
                }
                return RedirectToPage("RegistrationSuccessful", new { code = registrationCode });
            }
        }
    }
}