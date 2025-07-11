using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
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
                var path = "N/A";
                var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
                if (feature is not null)
                {
                    path = feature.OriginalPath + feature.OriginalQueryString;
                }
                var userName = ForumUser.Username ?? "N/A";
                _logger.Warning("Serving response code {code} to user {user} for path {path}.", ResponseStatusCode, userName, path);
            }

            Response.StatusCode = ResponseStatusCode ?? (int)HttpStatusCode.InternalServerError;
            if (Response.StatusCode >= 200 && Response.StatusCode <= 299)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }
    }
}
