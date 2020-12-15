using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Objects;
using System;
using System.Globalization;
using System.Linq;

namespace PhpbbInDotnet.Languages
{
    public class LanguageProvider
    {
        const string DEFAULT_LANGUAGE = "ro";

        private readonly Lazy<Translation> _basicText;

        public Translation BasicText => _basicText.Value;

        public LanguageProvider()
        {
            _basicText = new Lazy<Translation>(() => new Translation(nameof(BasicText)));
        }

        public string GetValidatedLanguage(LoggedUser user, HttpRequest request = null)
        {
            var fromHeadersOrDefault = ValidatedOrDefault(
                (request?.Headers?.TryGetValue("Accept-Language", out var val) ?? false) ? val.ToString() : DEFAULT_LANGUAGE, 
                DEFAULT_LANGUAGE
            );

            if (user?.IsAnonymous ?? true)
            {
                return fromHeadersOrDefault;
            }

            return ValidatedOrDefault(user.Language, fromHeadersOrDefault);
        }

        string ValidatedOrDefault(string language, string @default)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
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
            catch
            {
                return @default;
            }
        }
    }
}
