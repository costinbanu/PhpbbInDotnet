using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
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
                var path = "N/A";
                var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
                if (feature is not null)
                {
                    path = feature.OriginalPath + feature.OriginalQueryString;
                }
                var userName = AuthenticatedUserExpanded.GetValueOrDefault(HttpContext)?.Username ?? "N/A";
                _logger.Warning("Serving response code {code} to user {user} for path {path}.", ResponseStatusCode, userName, path);
            }
        }
    }
}
