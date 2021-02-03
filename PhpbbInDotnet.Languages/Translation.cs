﻿using LazyCache;
using Newtonsoft.Json;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace PhpbbInDotnet.Languages
{
    public abstract class Translation
    {
        static readonly CultureInfo EN = new CultureInfo("en");

        private readonly string _name;
        private readonly IAppCache _cache;
        private readonly ILogger _logger;

        protected abstract string FileExtension { get; }

        protected abstract bool ShouldCacheRawTranslation { get; }

        protected Translation(string name, ILogger logger, IAppCache cache)
        {
            _name = name;
            _cache = cache;
            _logger = logger;
        }

        public bool Exists(string language)
            => File.Exists(GetFile(language));

        protected string GetRawTranslation(string language)
        {
            string getValue()
            {
                var path = GetFile(language);
                if (!File.Exists(path))
                {
                    _logger.Warning("Potentially missing language: '{language}' - file '{path}' does not exist.", language, path);
                    return null;
                }
                return File.ReadAllText(path);
            }

            if (ShouldCacheRawTranslation)
            {
                var dummy = _cache.GetOrAdd(GetLanguageKey(language), getValue);
                return dummy;
            }

            var dummy2 = getValue();
            return dummy2;
        }

        protected string GetFromDictionary(string language, string key, Casing casing)
        {
            var dict = _cache.GetOrAdd(GetDictionaryKey(language), () =>
            {
                var value = GetDictionary(language); 
                if (value == null)
                {
                    value = GetDictionary(Constants.DEFAULT_LANGUAGE);
                    _logger.Warning("Dictionary for '{language}' is missing or corrupted.", language);
                }
                return value;
            });

            if (!dict.TryGetValue(key, out var toReturn))
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

        private ConcurrentDictionary<string, string> GetDictionary(string language)
        {
            var rawValue = GetRawTranslation(language);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }
            var value = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            try
            {
                JsonConvert.PopulateObject(rawValue, value);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to read translation '{_name}' for language '{language}' and parse its contents.", _name, language);
                return null;
            }

            return value;
        }

        private string GetFile(string language)
            => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations", $"{_name}.{language}.{FileExtension}");

        private string GetLanguageKey(string language)
            => $"LANG_{_name}_{language}";

        private string GetDictionaryKey(string language)
            => $"DICT_{_name}_{language}";
    }
}
