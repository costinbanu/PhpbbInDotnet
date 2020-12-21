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
        private readonly Lazy<Translation> _basicText;
        private readonly Lazy<Translation> _aboutCookies;
        private readonly Lazy<Translation> _email;
        private readonly ILogger _logger;

        public Translation BasicText => _basicText.Value;
        public Translation AboutCookies => _aboutCookies.Value;
        public Translation Email => _email.Value;

        public LanguageProvider(ILogger logger)
        {
            _logger = logger;
            _basicText = new Lazy<Translation>(() => new Translation(nameof(BasicText), _logger));
            _aboutCookies = new Lazy<Translation>(() => new Translation(nameof(AboutCookies), _logger));
            _email = new Lazy<Translation>(() => new Translation(nameof(Email), _logger));
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
                return new CultureInfo(language).TwoLetterISOLanguageName;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Unable to parse language code '{language}'.");
                return @default;
            }
        }
    }
}
