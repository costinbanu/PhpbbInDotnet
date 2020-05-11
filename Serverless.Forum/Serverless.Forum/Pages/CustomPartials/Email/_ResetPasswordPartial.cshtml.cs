using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials.Email
{
    public class _ResetPasswordPartialModel : PageModel
    {
        public string Code { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
    }
}