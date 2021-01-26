using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public abstract class MultilingualServiceBase
    {
        protected readonly LanguageProvider LanguageProvider;
        protected readonly IHttpContextAccessor HttpContextAccessor;
        protected readonly CommonUtils Utils;

        private string _language;

        public MultilingualServiceBase(CommonUtils utils, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
        {
            LanguageProvider = languageProvider;
            HttpContextAccessor = httpContextAccessor;
            Utils = utils;
        }

        protected async Task<string> GetLanguage()
            => _language ??= LanguageProvider.GetValidatedLanguage(
                user: await ClaimsPrincipalToAuthenticatedUser(HttpContextAccessor.HttpContext.User), 
                request: HttpContextAccessor.HttpContext.Request
            );

        protected async Task<AuthenticatedUser> ClaimsPrincipalToAuthenticatedUser(ClaimsPrincipal principal)
            => await Utils.DecompressObject<AuthenticatedUser>(Convert.FromBase64String(principal?.Claims?.FirstOrDefault()?.Value ?? string.Empty));
    }
}
