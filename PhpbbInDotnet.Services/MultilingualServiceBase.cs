using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Services
{
    public abstract class MultilingualServiceBase
    {
        protected readonly ITranslationProvider TranslationProvider;
        protected readonly IHttpContextAccessor HttpContextAccessor;
        protected readonly ICommonUtils Utils;

        private string? _language;

        public MultilingualServiceBase(ICommonUtils utils, ITranslationProvider translationProvider, IHttpContextAccessor httpContextAccessor)
        {
            TranslationProvider = translationProvider;
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

            _language = TranslationProvider.GetValidatedLanguage(user, HttpContextAccessor.HttpContext?.Request);

            return _language;
        }
    }
}
