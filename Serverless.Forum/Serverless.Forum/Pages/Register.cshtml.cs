using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PaulMiami.AspNetCore.Mvc.Recaptcha;

namespace Serverless.Forum.Pages
{
    [ValidateRecaptcha]
    [BindProperties]
    [ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(maximumLength: 32, MinimumLength = 2, ErrorMessage = "Username must be between 2 and 32 characters long.")]
        [RegularExpression(@"[a-zA-Z0-9 \._-]+", ErrorMessage = "Allowed characters in username: a-z, A-Z, 0-9, [space], [dot], [underrscore], [dash].")]
        public string UserName { get; set; }

        [Required]
        [StringLength(maximumLength: 256, MinimumLength = 8, ErrorMessage = "Password must me at least 8 characters long.")]
        public string Password { get; set; }

        [Required]
        [Compare(otherProperty: nameof(Password), ErrorMessage = "The two passwords must match.")]
        public string SecondPassword { get; set; }

        [Required]
        [Range(type: typeof(bool), minimum: "True", maximum: "True", ErrorMessage = "You must agree to the terms of service.")]
        public bool Agree { get; set; }

        public void OnPost()
        {
            
        }
    }
}