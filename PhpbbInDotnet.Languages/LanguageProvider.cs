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

        public LanguageProvider(ILogger logger)
        {
            _logger = logger;
            _basicText = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BasicText), _logger));
            _aboutCookies = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(AboutCookies), _logger));
            _email = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Email), _logger));
            _enums = new Lazy<EnumTranslation>(() => new EnumTranslation(_logger));
            _jsText = new Lazy<JavaScriptTranslation>(() => new JavaScriptTranslation(_logger));
            _faq = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(FAQ), _logger));
            _errors = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Errors), _logger));
            _postingGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(PostingGuide), _logger));
            _termsAndConditions = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(TermsAndConditions), _logger));
            _moderator = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Moderator), _logger));
        }

        public string GetValidatedLanguage(LoggedUser user, HttpRequest request = null)
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
                if (!(BasicText.Exists(toReturn) && AboutCookies.Exists(toReturn) && Email.Exists(toReturn) && Enums.Exists(toReturn) && 
                    JSText.Exists(toReturn) && FAQ.Exists(toReturn) && Errors.Exists(toReturn) && PostingGuide.Exists(toReturn)))
                {
                    return @default;
                }
                return toReturn;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Unable to parse language code '{language}'.");
                return @default;
            }
        }
    }
}
