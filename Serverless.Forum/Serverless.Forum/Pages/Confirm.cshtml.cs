using Microsoft.AspNetCore.Mvc;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Serverless.Forum.Pages
{
    public class ConfirmModel : ModelWithLoggedUser
    {
        private readonly Utils _utils;
        private readonly IConfiguration _config;

        public string Message { get; private set; }
        
        public string Title { get; private set; }
        
        [BindProperty(SupportsGet = true)]
        public int? ForumId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorPostActions? PostAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool ShowTopicSelector { get; set; }

        [BindProperty(SupportsGet = true)]
        public List<string> Destinations { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SelectedPostIds { get; set; }

        public bool IsModeratorConfirmation { get; private set; } = false;

        public bool IsDestinationConfirmation { get; private set; } = false;

        public ConfirmModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, Utils utils, IConfiguration config)
            : base(context, forumService, userService, cacheService) 
        {
            _utils = utils;
            _config = config;
        }

        public void OnGetRegistrationComplete()
        {
            Message =
                "Mulțumim pentru înregistrare! Ți-am trimis un e-mail pe adresa furnizată ce conține instrucțiuni pentru pașii următori. " +
                "Verifică-ți căsuța de email, inclusiv folderele pentru \"spam\", și urmează instrucțiunile din mesajul primit. " +
                "Dacă nu ai primit nici un mesaj, te rugăm să contactezi echipa administrativă la <a href=\"mailto:admin@metrouusor.com\">admin@metrouusor.com</a>.";

            Title = "Confirmarea înregistrării";
        }

        public async Task OnGetConfirmEmail(string code, string username)
        {
            var user = await _context.PhpbbUsers.FirstOrDefaultAsync(u =>
                u.UsernameClean == username &&
                u.UserActkey == code &&
                u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed
            );

            if (user == null)
            {
                Message =
                    "<span style=\"color=red\">" +
                        "Înregistrarea nu poate fi confirmată (utilizatorul nu există, este deja activ sau nu este asociat codului de activare).<br/>" +
                        "Contactează administratorul pentru mai multe detalii." +
                    "</span>";
            }
            else
            {
                Message =
                    "<span style=\"color=darkgreen\">" +
                        "Înregistrarea a fost confirmată cu succes!<br/>" +
                        "Contul va fi activat în următoarele 48 de ore. După activare vei putea face login pe forumul nostru.<br/>" +
                        "Contactează echipa administrativă la <a href=\"mailto:admin@metrouusor.com\">admin@metrouusor.com</a> " +
                        "dacă au trecut mai mult de 48 de ore de la înregistrare iar contul încă nu a fost activat." +
                    "</span>";

                user.UserInactiveReason = UserInactiveReason.NewlyRegisteredConfirmed;
                user.UserActkey = string.Empty;
                await _context.SaveChangesAsync();

                var subject = "Cont de utilizator nou";
                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
                    Subject = subject,
                    Body = await _utils.RenderRazorViewToString(
                        "_NewUserNotification",
                        new _NewUserNotificationModel(user.Username),
                        PageContext,
                        HttpContext
                    ),
                    IsBodyHtml = true
                };

                var adminEmails = await (
                    from u in _context.PhpbbUsers.AsNoTracking()
                    join ug in _context.PhpbbUserGroup.AsNoTracking()
                    on u.UserId equals ug.UserId
                    into joined
                    from j in joined
                    where j.GroupId == Constants.ADMIN_GROUP_ID && !string.IsNullOrWhiteSpace(u.UserEmail)
                    select u.UserEmail.Trim()
                ).ToListAsync();

                emailMessage.To.Add(string.Join(',', adminEmails));
                await _utils.SendEmail(emailMessage);
            }
            Title = "Confirmarea adresei de e-mail";
        }

        public void OnGetNewPassword()
        {
                Message =
                    "<span style=\"color=darkgreen\">" +
                        "Am trimis un e-mail, la adresa completată anterior, cu mai multe instrucțiuni pe care trebuie să le urmezi ca să îți poți recupera contul." + 
                    "</span>";

            Title = "Modificarea parolei";
        }

        public void OnGetPasswordChanged()
        {
            Message =
                "<span style=\"color=darkgreen\">" +
                    "Parola a fost modificată cu succes." +
                "</span>";

            Title = "Modificarea parolei";
        }

        public void OnGetModeratorConfirmation()
        {
            IsModeratorConfirmation = true;
            if (ShowTopicSelector)
            {
                Title = "Alege forumul și subiectul de destinație";
            }
            else
            {
                Title = "Alege forumul de destinație";
            }
        }

        public void OnGetDestinationConfirmation()
        {
            IsDestinationConfirmation = true;
            Message =
                "<span style=\"color=darkgreen\">" +
                    "Operațiunea a fost efectuată cu succes." +
                "</span>";
        }
    }
}