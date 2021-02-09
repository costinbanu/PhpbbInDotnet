using System;

namespace PhpbbInDotnet.Objects
{
    public class WelcomeEmailDto
    {
        public string Subject { get; set; }
        public string RegistrationCode { get; set; }
        public string UserName { get; set; }
        public bool IsRegistrationReminder { get; set; } = false;
        public DateTime? RegistrationDate { get; set; } = null;
        public bool IsEmailChangeReminder { get; set; } = false;
        public DateTime? EmailChangeDate { get; set; } = null;
    }
}
