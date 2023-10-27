using System.Collections.Generic;

namespace PhpbbInDotnet.Objects.Configuration
{
	public class SmtpConfig
	{
		public string Host { get; set; } = string.Empty;
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public bool EnableSsl { get; set; }
		public int Port { get; set; }
		public List<string> AllowedReceivers { get; set; } = new();
	}
}
