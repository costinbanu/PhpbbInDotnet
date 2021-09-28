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
        {
            if (!string.IsNullOrWhiteSpace(_language))
            {
                return _language;
            }

            AuthenticatedUser user = null;
            if (HttpContextAccessor.HttpContext != null)
            {
                user = (AuthenticatedUser)(HttpContextAccessor.HttpContext.Items.TryGetValue(nameof(AuthenticatedUser), out var val) ? val : null);
            }

            _language = LanguageProvider.GetValidatedLanguage(user, HttpContextAccessor.HttpContext?.Request);

            return _language;
        }
    }
}
