using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using Serilog;
using System.Net;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ErrorModel : BaseModel
    {
        private readonly ILogger _logger;

        [BindProperty(SupportsGet = true)]
        public string? ErrorId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CustomErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ResponseStatusCode { get; set; }

        public bool IsUnauthorized => ResponseStatusCode == (int)HttpStatusCode.Unauthorized;

        public bool IsNotFound => ResponseStatusCode == (int)HttpStatusCode.NotFound;

        public ErrorModel(ILogger logger, ITranslationProvider translationProvider, IUserService userService, IConfiguration configuration)
            : base(translationProvider, userService, configuration)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            if (ResponseStatusCode is not null)
            {
                string? path = null;
                var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
                if (feature is not null)
                {
                    path = feature.OriginalPath + feature.OriginalQueryString;
                }
                if (!string.IsNullOrWhiteSpace(path) && ForumUser.UserId != Constants.ANONYMOUS_USER_ID)
                {
                    _logger.Warning("Serving response code {code} to user {user} for path {path}.", ResponseStatusCode, ForumUser.Username, path);
                }
            }
        }
    }
}
