using System;

namespace PhpbbInDotnet.Objects.EmailDtos
{
	public class ResetPasswordDto : SimpleEmailBody
	{
		public ResetPasswordDto(string code, int userId, string userName, Guid iv, string language)
			: base(language, userName)
		{
			Code = code;
			UserId = userId;
			IV = iv;
		}
		public string Code { get; }
		public int UserId { get; }
		public Guid IV { get; }
	}
}