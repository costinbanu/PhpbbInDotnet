using LazyCache;
using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Globalization;
using System.Linq;

namespace PhpbbInDotnet.Languages
{
    public class LanguageProvider
    {
        private readonly ILogger _logger;

        private readonly Lazy<TextTranslation> _basicText;
        private readonly Lazy<HtmlTranslation> _aboutCookies;
        private readonly Lazy<TextTranslation> _email;
        private readonly Lazy<EnumTranslation> _enums;
        private readonly Lazy<JavaScriptTranslation> _jsText;
        private readonly Lazy<HtmlTranslation> _faq;
        private readonly Lazy<TextTranslation> _errors;
        private readonly Lazy<HtmlTranslation> _postingGuide;
        private readonly Lazy<HtmlTranslation> _termsAndConditions;
        private readonly Lazy<TextTranslation> _moderator;
        private readonly Lazy<TextTranslation> _admin;
        private readonly Lazy<HtmlTranslation> _customBBCodeGuide;
        private readonly Lazy<HtmlTranslation> _attachmentGuide;
        private readonly Lazy<TextTranslation> _bbCodes;

        public TextTranslation BasicText => _basicText.Value;

        public HtmlTranslation AboutCookies => _aboutCookies.Value;

        public TextTranslation Email => _email.Value;

        public EnumTranslation Enums => _enums.Value;

        public JavaScriptTranslation JSText => _jsText.Value;

        public HtmlTranslation FAQ => _faq.Value;

        public TextTranslation Errors => _errors.Value;

        public HtmlTranslation PostingGuide => _postingGuide.Value;

        public HtmlTranslation TermsAndConditions => _termsAndConditions.Value;

        public TextTranslation Moderator => _moderator.Value;

        public TextTranslation Admin => _admin.Value;

        public HtmlTranslation CustomBBCodeGuide => _customBBCodeGuide.Value;

        public HtmlTranslation AttachmentGuide => _attachmentGuide.Value;

        public TextTranslation BBCodes => _bbCodes.Value;

        public LanguageProvider(ILogger logger, IAppCache cache)
        {
            _logger = logger;
            _basicText = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BasicText), _logger, cache));
            _aboutCookies = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(AboutCookies), _logger, cache));
            _email = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Email), _logger, cache));
            _enums = new Lazy<EnumTranslation>(() => new EnumTranslation(_logger, cache));
            _jsText = new Lazy<JavaScriptTranslation>(() => new JavaScriptTranslation(_logger, cache));
            _faq = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(FAQ), _logger, cache));
            _errors = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Errors), _logger, cache));
            _postingGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(PostingGuide), _logger, cache));
            _termsAndConditions = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(TermsAndConditions), _logger, cache));
            _moderator = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Moderator), _logger, cache));
            _admin = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Admin), _logger, cache));
            _customBBCodeGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(CustomBBCodeGuide), _logger, cache));
            _attachmentGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(AttachmentGuide), _logger, cache));
            _bbCodes = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BBCodes), _logger, cache));
        }

        public string GetValidatedLanguage(AuthenticatedUser user, HttpRequest request = null)
        {
            var fromHeadersOrDefault = ValidatedOrDefault(
                (request?.Headers?.TryGetValue("Accept-Language", out var val) ?? false) ? val.ToString() : Constants.DEFAULT_LANGUAGE, 
                Constants.DEFAULT_LANGUAGE
            );

            if (user?.IsAnonymous ?? true)
            {
                return fromHeadersOrDefault;
            }

            return ValidatedOrDefault(user.Language, fromHeadersOrDefault);
        }

        private string ValidatedOrDefault(string language, string @default)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                _logger.Warning("Received empty language code");
                return @default;
            }

            if (language.Contains(','))
            {
                language = language.Split(",").First().Trim();
            }

            try
            {
                var toReturn = new CultureInfo(language).TwoLetterISOLanguageName;

                if (new Translation[] { BasicText, AboutCookies, Email, Enums, JSText, FAQ, Errors, PostingGuide, TermsAndConditions, Moderator, Admin, CustomBBCodeGuide, AttachmentGuide, BBCodes }
                    .Any(x => !x.Exists(toReturn)))
                {
                    _logger.Warning("Language '{language}' was requested, but it does not exist.", language);
                    return @default;
                }

                return toReturn;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Unable to parse language code '{language}'.", language);
                return @default;
            }
        }
    }
}
