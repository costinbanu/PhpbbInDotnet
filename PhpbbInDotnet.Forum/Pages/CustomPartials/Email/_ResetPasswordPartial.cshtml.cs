using PhpbbInDotnet.Objects;
using System;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Email
{
    public class _ResetPasswordPartialModel : SimpleEmailBody
    {
        public _ResetPasswordPartialModel(string code, int userId, string userName, Guid iv, string language)
            : base(userName, language)
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