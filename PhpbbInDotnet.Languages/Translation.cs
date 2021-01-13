using Newtonsoft.Json;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace PhpbbInDotnet.Languages
{
    public abstract class Translation
    {
        static readonly CultureInfo EN = new CultureInfo("en");

        private readonly string _name;
        private readonly Dictionary<string, Dictionary<string, string>> _dictionary;
        private readonly Dictionary<string, string> _rawCache;
        private readonly ILogger _logger;

        private string _path;

        protected abstract string FileExtension { get; }

        protected abstract bool ShouldCacheRawTranslation { get; }

        protected Translation(string name, ILogger logger)
        {
            _name = name;
            _dictionary = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
            _rawCache = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _logger = logger;
        }

        public bool Exists(string language)
            => File.Exists(GetFile(language));

        protected string GetRawTranslation(string language)
        {
            if (ShouldCacheRawTranslation && _rawCache.ContainsKey(language))
            {
                return _rawCache[language];
            }

            var path = GetFile(language);
            if (!File.Exists(path))
            {
                _logger.Warning($"Potentially missing language: '{language}' - file '{path}' does not exist.");
                return null;
            }
            var value = File.ReadAllText(path);
            if (ShouldCacheRawTranslation)
            {
                _rawCache.Add(language, value);
            }
            return value;
        }

        protected string GetFromDictionary(string language, string key, Casing casing)
        {
            if (!_dictionary.ContainsKey(language))
            {
                var value = GetDictionary(language);
                if (value == null)
                {
                    _logger.Warning($"Switching to default language '{Constants.DEFAULT_LANGUAGE}'...");
                    language = Constants.DEFAULT_LANGUAGE;
                    if (!_dictionary.ContainsKey(language))
                    {
                        _dictionary.Add(language, GetDictionary(language));
                    }
                }
                else
                {
                    _dictionary.Add(language, value);
                }
            }

            if (!_dictionary[language].TryGetValue(key, out var toReturn))
            {
                return key;
            }

            return casing switch
            {
                Casing.AllLower => toReturn.ToLower(),
                Casing.AllUpper => toReturn.ToUpper(),
                Casing.FirstUpper => FirstUpper(toReturn),
                Casing.Title => TitleCase(language, toReturn),
                Casing.None => toReturn,
                _ => toReturn
            };
        }

        private string FirstUpper(string text)
            => char.ToUpper(text[0]) + text[1..].ToLower();

        private string TitleCase(string language, string text)
            => language.Equals("en", StringComparison.InvariantCultureIgnoreCase) ? EN.TextInfo.ToTitleCase(text) : FirstUpper(text);

        private Dictionary<string, string> GetDictionary(string language)
        {
            var rawValue = GetRawTranslation(language);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }
            var value = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            try
            {
                JsonConvert.PopulateObject(rawValue, value);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Failed to read translation '{_name}' for language '{language}' and parse its contents.");
                return null;
            }

            return value;
        }

        private string GetFile(string language)
            => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations", $"{_name}.{language}.{FileExtension}");
    }
}
