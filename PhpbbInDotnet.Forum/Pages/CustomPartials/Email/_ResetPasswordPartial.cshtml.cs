using System;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Email
{
    public class _ResetPasswordPartialModel : PageModel
    {
        public string Code { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public Guid IV { get; set; }
    }
}