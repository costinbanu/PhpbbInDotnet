using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class ConfirmModel : PageModel
    {
        private readonly ForumDbContext _context;

        public string Message { get; private set; }
        public string Title { get; private set; }

        public ConfirmModel(ForumDbContext context)
        {
            _context = context;
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
    }
}