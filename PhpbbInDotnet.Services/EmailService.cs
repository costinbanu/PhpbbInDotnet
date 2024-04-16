using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MimeKit;
using MimeKit.Text;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects.Configuration;
using Serilog;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class EmailService : IEmailService
    {
        private readonly IRazorViewService _razorViewService;
		private readonly IWebHostEnvironment _environment;
		private readonly SmtpConfig _smtpConfig;
		private readonly string _adminEmail;
		private readonly ILogger _logger;

		public EmailService(IConfiguration config, IRazorViewService razorViewService, IWebHostEnvironment environment, ILogger logger)
        {
			_razorViewService = razorViewService;
            _environment = environment;
            _smtpConfig = config.GetObject<SmtpConfig>("Smtp");
            _adminEmail = config.GetValue<string>("AdminEmail")!;
            _logger = logger;
		}

        public async Task SendEmail(string to, string subject, string bodyRazorViewName, object bodyRazorViewModel)
        {
            if (!_environment.IsProduction() && !_smtpConfig.AllowedReceivers.Contains(to))
            {
                _logger.Warning("An attempt to send an email with subject '{subject}' to the external receiver '{to}' while testing in '{environment}' was blocked. The email was not sent.", subject, to, _environment.EnvironmentName);
                return;
            }

            using var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_adminEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html)
            {
                Text = await _razorViewService.RenderRazorViewToString(bodyRazorViewName, bodyRazorViewModel)
            };
            using var smtp = new SmtpClient();
            smtp.Connect(_smtpConfig.Host, _smtpConfig.Port, SecureSocketOptions.SslOnConnect);
            smtp.Authenticate(_smtpConfig.Username, _smtpConfig.Password);
            await smtp.SendAsync(email);
            smtp.Disconnect(true);
        }
    }
}
