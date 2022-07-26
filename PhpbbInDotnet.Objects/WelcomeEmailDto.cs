using System;

namespace PhpbbInDotnet.Objects
{
    public class WelcomeEmailDto : SimpleEmailBody
    {
        public WelcomeEmailDto(string subject, string registrationCode, string userName, string language)
            : base(userName, language)
        {
            Subject = subject;
            RegistrationCode = registrationCode;
        }

        public string Subject { get; }

        public string RegistrationCode { get; }

        public bool IsRegistrationReminder { get; set; }

        public DateTime? RegistrationDate { get; set; }

        public bool IsEmailChangeReminder { get; set; }

        public DateTime? EmailChangeDate { get; set; }
    }
}
