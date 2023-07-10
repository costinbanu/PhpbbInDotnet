using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Database.Entities;

namespace PhpbbInDotnet.Languages
{
    class TranslationProvider : ITranslationProvider
    {
        private string? _language;
        private IEnumerable<string>? _allLanguages;

        private readonly ILogger _logger;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IAppCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;

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
        private readonly Lazy<TextTranslation> _bbCodes;
        private readonly Lazy<HtmlTranslation> _externalLinks;

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

        public TextTranslation BBCodes => _bbCodes.Value;

        public HtmlTranslation ExternalLinks => _externalLinks.Value;

        #endregion Translation declarations

        public TranslationProvider(ILogger logger, IAppCache cache, ISqlExecuter sqlExecuter, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _sqlExecuter = sqlExecuter;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;

            #region Translation init

            _basicText = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BasicText), _logger, cache));
            _aboutCookies = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(AboutCookies), _logger, cache));
            _email = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Email), _logger, cache));
            _enums = new Lazy<EnumTranslation>(() => new EnumTranslation(_logger, cache));
            _jsText = new Lazy<JavaScriptTranslation>(() => new JavaScriptTranslation(_logger, cache));
            _errors = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Errors), _logger, cache));
            _postingGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(PostingGuide), _logger, cache));
            _termsAndConditions = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(TermsAndConditions), _logger, cache));
            _moderator = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Moderator), _logger, cache));
            _admin = new Lazy<TextTranslation>(() => new TextTranslation(nameof(Admin), _logger, cache));
            _customBBCodeGuide = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(CustomBBCodeGuide), _logger, cache));
            _bbCodes = new Lazy<TextTranslation>(() => new TextTranslation(nameof(BBCodes), _logger, cache));
            _externalLinks = new Lazy<HtmlTranslation>(() => new HtmlTranslation(nameof(ExternalLinks), _logger, cache));

            #endregion Translation init
        }

        public string GetLanguage(ForumUserExpanded? user = null)
        {
            if (_language is not null)
            {
                return _language;
            }

            var fromHeadersOrDefault = Constants.DEFAULT_LANGUAGE;
            if (_httpContextAccessor.HttpContext is not null)
            {
                fromHeadersOrDefault = ValidatedOrDefault(
                    _httpContextAccessor.HttpContext.Request.Headers.TryGetValue("Accept-Language", out StringValues lang)? lang.ToString() : Constants.DEFAULT_LANGUAGE,
                    Constants.DEFAULT_LANGUAGE);
                user ??= ForumUserExpanded.GetValueOrDefault(_httpContextAccessor.HttpContext);
            }

            if (user?.IsAnonymous != false)
            {
                return _language = fromHeadersOrDefault;
            }

            return _language = ValidatedOrDefault(user.Language, fromHeadersOrDefault);
        }

        private bool IsLanguageValid(string language, [MaybeNullWhen(false)] out string parsed)
        {
            var (valid, toReturn) = _cache.GetOrAdd<(bool, string?)>(
                  $"{nameof(IsLanguageValid)}_{language}",
                  () =>
                  {
                      try
                      {
                          var parsed = new CultureInfo(language).TwoLetterISOLanguageName;
                          
                          if (_sqlExecuter.ExecuteScalar<bool>("SELECT count(1) FROM phpbb_lang WHERE lang_iso = @parsed", new { parsed }) &&
                              new Translation[] { BasicText, AboutCookies, Email, Enums, JSText, Errors, PostingGuide, TermsAndConditions, Moderator, Admin, CustomBBCodeGuide, BBCodes }
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
            parsed = toReturn;
            return valid;
        }

        private string ValidatedOrDefault(string? language, string @default)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return @default;
            }

            language = language.Split(',', ';').First().Trim();
            var isValid = IsLanguageValid(language, out var twoLetterLanguageName);
            if (!isValid && language.Length >= 2)
            {
                isValid = IsLanguageValid(language.Substring(0, 2), out twoLetterLanguageName);
            }
            return isValid ? twoLetterLanguageName! : @default;
        }

        private static readonly char[] DATE_FORMATS = new[] { 'f', 'g' };

        public async Task<Dictionary<string, List<string>>> GetDateFormatsInAllLanguages()
            => (await _sqlExecuter.QueryAsync<PhpbbLang>("SELECT * FROM phpbb_lang")).ToDictionary(lang => lang.LangIso, lang => GetDateFormats(lang.LangIso));

        public string GetDefaultDateFormat(string lang)
            => GetDateFormats(lang).FirstOrDefault() ?? "dddd, dd MMM yyyy, HH:mm";

        public List<string> GetDateFormats(string lang)
        {
            var culture = new CultureInfo(lang);
            return DATE_FORMATS.Select(x => culture.DateTimeFormat.GetAllDateTimePatterns(x)).SelectMany(x => x).Distinct().ToList();
        }

        public IEnumerable<string> AllLanguages
            => _allLanguages ??= Directory.GetFiles(Translation.TranslationsDirectory).Where(IsBasicText).Select(TranslationLanguage);

        private bool IsBasicText(string path)
        {
            var file = Path.GetFileNameWithoutExtension(path);
            var dotIndex = file.IndexOf('.');
            return file[..dotIndex] == _basicText.Value.Name;
        }

        private string TranslationLanguage(string path)
        {
            var file = Path.GetFileNameWithoutExtension(path);
            var dotIndex = file.IndexOf('.');
            return file[(dotIndex + 1)..];
        }
    }
}
