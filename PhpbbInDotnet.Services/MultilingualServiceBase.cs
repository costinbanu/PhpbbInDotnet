using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;

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

        protected string GetLanguage()
            => _language ??= LanguageProvider.GetValidatedLanguage(
                user: (AuthenticatedUser)(HttpContextAccessor.HttpContext.Items.TryGetValue(nameof(AuthenticatedUser), out var val) ? val : null), 
                request: HttpContextAccessor.HttpContext.Request
            );
    }
}
