using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Languages
{
    public class LanguageProvider
    {
        private readonly ILogger _logger;
        private readonly ForumDbContext _context;
        private readonly IAppCache _cache;

        #region Translation declarations

        private readonly Lazy<TextTranslation> _basicText;
        private readonly Lazy<HtmlTranslation> _aboutCookies;
        private readonly Lazy<TextTranslation> _email;
        private readonly Lazy<EnumTranslation> _enums;
        private readonly Lazy<JavaScriptTranslation> _jsText;
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

        public TextTranslation Errors => _errors.Value;

        public HtmlTranslation PostingGuide => _postingGuide.Value;

        public HtmlTranslation TermsAndConditions => _termsAndConditions.Value;

        public TextTranslation Moderator => _moderator.Value;

        public TextTranslation Admin => _admin.Value;

        public HtmlTranslation CustomBBCodeGuide => _customBBCodeGuide.Value;

        public HtmlTranslation AttachmentGuide => _attachmentGuide.Value;

        public TextTranslation BBCodes => _bbCodes.Value;

        #endregion Translation declarations

        public LanguageProvider(ILogger logger, IAppCache cache, ForumDbContext context, CommonUtils utils)
        {
            _logger = logger;
            _context = context;
            _cache = cache;

            #region Translation init

            _basicText = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BasicText), _logger, cache));
            _aboutCookies = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(AboutCookies), _logger, cache));
            _email = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Email), _logger, cache));
            _enums = new Lazy<EnumTranslation>(() => new EnumTranslation(_logger, cache, utils));
            _jsText = new Lazy<JavaScriptTranslation>(() => new JavaScriptTranslation(_logger, cache));
            _errors = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Errors), _logger, cache));
            _postingGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(PostingGuide), _logger, cache));
            _termsAndConditions = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(TermsAndConditions), _logger, cache));
            _moderator = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Moderator), _logger, cache));
            _admin = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Admin), _logger, cache));
            _customBBCodeGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(CustomBBCodeGuide), _logger, cache));
            _attachmentGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(AttachmentGuide), _logger, cache));
            _bbCodes = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BBCodes), _logger, cache));

            #endregion Translation init
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

        public (bool isValid, string twoLetterLanguageName) IsLanguageValid(string language)
            => _cache.GetOrAdd(
                $"{nameof(IsLanguageValid)}_{language}",
                () =>
                {
                    try
                    {
                        var parsed = new CultureInfo(language).TwoLetterISOLanguageName;

                        if (_context.PhpbbLang.AsNoTracking().Any(lang => lang.LangIso == parsed) &&
                            new Translation[] { BasicText, AboutCookies, Email, Enums, JSText, Errors, PostingGuide, TermsAndConditions, Moderator, Admin, CustomBBCodeGuide, AttachmentGuide, BBCodes }
                            .All(x => x.Exists(parsed)))
                        {
                            return (true, parsed);
                        }
                        _logger.Warning("Language '{language}' was requested, but it does not exist.", parsed);
                        return (false, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Unable to parse language code '{language}'.", language);
                        return (false, null);
                    }
                },
                Translation.CACHE_EXPIRATION
            );

        private string ValidatedOrDefault(string language, string @default)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return @default;
            }

            language = language.Split(',', ';').First().Trim();
            var (isValid, twoLetterLanguageName) = IsLanguageValid(language);
            if (!isValid && language.Length >= 2)
            {
                (isValid, twoLetterLanguageName) = IsLanguageValid(language.Substring(0, 2));
            }
            return isValid ? twoLetterLanguageName : @default;
        }

        private static readonly char[] DATE_FORMATS = new[] { 'f', 'g' };

        public async Task<Dictionary<string, List<string>>> GetDateFormatsInAllLanguages()
            => (await _context.PhpbbLang.AsNoTracking().ToListAsync()).ToDictionary(lang => lang.LangIso, lang => GetDateFormats(lang.LangIso));

        public string GetDefaultDateFormat(string lang)
            => GetDateFormats(lang).FirstOrDefault() ?? "dddd, dd MMM yyyy, HH:mm";

        public List<string> GetDateFormats(string lang)
        {
            var culture = new CultureInfo(lang);
            return DATE_FORMATS.Select(x => culture.DateTimeFormat.GetAllDateTimePatterns(x)).SelectMany(x => x).Distinct().ToList();
        }
    }
}
