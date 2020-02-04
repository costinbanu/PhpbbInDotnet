using CryptSharp.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaulMiami.AspNetCore.Mvc.Recaptcha;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Utilities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    [ValidateRecaptcha]
    [BindProperties]
    [ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

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

        public RegisterModel(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
        }

        public async Task<IActionResult> OnPost()
        {
            using (var context = new ForumDbContext(_config))
            {
                var check = await (from u in context.PhpbbUsers
                                   where u.UsernameClean == _utils.CleanString(UserName)
                                      || u.UserEmail == Email
                                   select new { u.UsernameClean, u.UserEmail })
                                  .ToListAsync();

                if (check.Any(u => u.UsernameClean == _utils.CleanString(UserName)))
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

                var now = DateTime.UtcNow.ToUnixTimestamp();
                var userInsertResult = await context.PhpbbUsers.AddAsync(new PhpbbUsers
                {
                    Username = UserName,
                    UsernameClean = _utils.CleanString(UserName),
                    UserEmail = Email,
                    UserPassword = Crypter.Phpass.Crypt(Password, Crypter.Phpass.GenerateSalt()),
                    UserInactiveTime = now,
                    UserInactiveReason = UserInactiveReason.NewlyRegisteredNotConfirmed,
                    UserActkey = registrationCode,
                    UserIp = HttpContext.Connection.RemoteIpAddress.ToString(),
                    UserRegdate = now,
                    UserLastmark = now
                });

                userInsertResult.Entity.UserId = 0;
                await context.SaveChangesAsync();

                await context.PhpbbUserGroup.AddAsync(new PhpbbUserGroup
                {
                    GroupId = 2,
                    UserId = userInsertResult.Entity.UserId,
                });

                await context.SaveChangesAsync();

                var subject = $"Bine ai venit la \"{Constants.FORUM_NAME}\"";
                var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", Constants.FORUM_NAME),
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
        }
    }
}