using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly IRazorViewService _razorViewService;

        public EmailService(IConfiguration config, IRazorViewService razorViewService)
        {
            _config = config;
            _razorViewService = razorViewService;
        }

        public async Task SendEmail(string to, string subject, string bodyRazorViewName, object bodyRazorViewModel)
        {
            
            using var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config.GetValue<string>("AdminEmail")));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html)
            {
                Text = await _razorViewService.RenderRazorViewToString(bodyRazorViewName, bodyRazorViewModel)
            };
            using var smtp = new SmtpClient();
            smtp.Connect(_config.GetValue<string>("Smtp:Host"), _config.GetValue<int>("Smtp:Port"), SecureSocketOptions.SslOnConnect);
            smtp.Authenticate(_config.GetValue<string>("Smtp:Username"), _config.GetValue<string>("Smtp:Password"));
            await smtp.SendAsync(email);
            smtp.Disconnect(true);
        }
    }
}
