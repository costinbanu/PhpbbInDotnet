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
        protected readonly ICommonUtils Utils;

        private string? _language;

        public MultilingualServiceBase(ICommonUtils utils, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
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

            AuthenticatedUserExpanded? user = null;
            if (HttpContextAccessor.HttpContext != null)
            {
                user = (AuthenticatedUserExpanded?)(HttpContextAccessor.HttpContext.Items.TryGetValue(nameof(AuthenticatedUserExpanded), out var val) ? val : null);
            }

            _language = LanguageProvider.GetValidatedLanguage(user, HttpContextAccessor.HttpContext?.Request);

            return _language;
        }
    }
}
