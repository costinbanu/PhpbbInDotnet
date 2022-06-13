using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Services;
using Serilog;
using System.Net;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ErrorModel : PageModel
    {
        private readonly ILogger _logger;
        private readonly IUserService _userService;

        [BindProperty(SupportsGet = true)]
        public string? ErrorId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CustomErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ResponseStatusCode { get; set; }

        public bool IsUnauthorized => ResponseStatusCode == (int)HttpStatusCode.Unauthorized;

        public bool IsNotFound => ResponseStatusCode == (int)HttpStatusCode.NotFound;

        public ErrorModel(ILogger logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        public void OnGet()
        {
            if (ResponseStatusCode is not null)
            {
                var userName = _userService.ClaimsPrincipalToAuthenticatedUser(User)?.Username ?? "N/A";
                _logger.Warning("Serving response code {code} to user {user}.", ResponseStatusCode, userName);
            }
        }
    }
}
