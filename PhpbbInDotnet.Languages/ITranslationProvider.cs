using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Languages
{
    public interface ITranslationProvider
    {
        HtmlTranslation AboutCookies { get; }
        TextTranslation Admin { get; }
        IEnumerable<string> AllLanguages { get; }
        HtmlTranslation AttachmentGuide { get; }
        TextTranslation BasicText { get; }
        TextTranslation BBCodes { get; }
        HtmlTranslation CustomBBCodeGuide { get; }
        TextTranslation Email { get; }
        EnumTranslation Enums { get; }
        TextTranslation Errors { get; }
        HtmlTranslation ExternalLinks { get; }
        JavaScriptTranslation JSText { get; }
        TextTranslation Moderator { get; }
        HtmlTranslation PostingGuide { get; }
        HtmlTranslation TermsAndConditions { get; }

        List<string> GetDateFormats(string lang);
        Task<Dictionary<string, List<string>>> GetDateFormatsInAllLanguages();
        string GetDefaultDateFormat(string lang);
        string GetValidatedLanguage(AuthenticatedUserExpanded? user, HttpRequest? request = null);
    }
}