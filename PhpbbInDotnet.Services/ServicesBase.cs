using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Languages;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public abstract class ServicesBase
    {
        protected readonly LanguageProvider LanguageProvider;
        protected readonly IHttpContextAccessor HttpContextAccessor;
        protected readonly UserService UserService;

        private string _language;

        public ServicesBase(UserService userService, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
        {
            LanguageProvider = languageProvider;
            HttpContextAccessor = httpContextAccessor;
            UserService = userService;
        }

        protected async Task<string> GetLanguage()
            => _language ??= LanguageProvider.GetValidatedLanguage(
                user: await UserService.ClaimsPrincipalToLoggedUserAsync(HttpContextAccessor.HttpContext.User), 
                request: HttpContextAccessor.HttpContext.Request
            );
    }
}
