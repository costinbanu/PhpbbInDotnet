using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class ConfirmModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public string Message { get; private set; }
        public string Title { get; private set; }

        public ConfirmModel(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
        }

        public void OnGetRegistrationComplete()
        {
            Message = 
                "Mulțumim pentru înregistrare! V-am trimis un e-mail pe adresa furnizată ce conține instrucțiuni pentru pașii următori. " +
                "Verificați-vă căsuța de email, inclusiv folderele pentru \"spam\", și urmați instrucțiunile din mesajul primit. " +
                "Dacă nu ați primit nici un mesaj, vă rugăm să contactați echipa administrativă la <a href=\"mailto:admin@metrouusor.com\">admin@metrouusor.com</a>.";

            Title = "Confirmarea înregistrării";
        }

        public async Task OnGetConfirmEmail(string code, string username)
        {
            using (var context = new ForumDbContext(_config))
            {
                var user = await context.PhpbbUsers.FirstOrDefaultAsync(u =>
                    u.UsernameClean == username &&
                    u.UserActkey == code &&
                    u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed
                );

                if (user == null)
                {
                    Message =
                        "<span style=\"color=red\">" +
                            "Înregistrarea nu poate fi confirmată (utilizatorul nu există, este deja activ sau nu este asociat codului de activare).<br/>" +
                            "Contactați administratorul pentru mai multe detalii." +
                        "</span>";
                }
                else
                {
                    Message =
                        "<span style=\"color=darkgreen\">" +
                            "Înregistrarea a fost confirmată cu succes!<br/>" +
                            "Contul va fi activat în următoarele 48 de ore. După activare veți putea face login pe forumul nostru.<br/>" +
                            "Contactați echipa administrativă la <a href=\"mailto:admin@metrouusor.com\">admin@metrouusor.com</a> " +
                            "dacă au trecut mai mult de 48 de ore de la înregistrare iar contul încă nu a fost activat." +
                        "</span>";

                    user.UserInactiveReason = UserInactiveReason.NewlyRegisteredConfirmed;
                    await context.SaveChangesAsync();
                }
            }
            Title = "Confirmarea adresei de e-mail";
        }
    }
}