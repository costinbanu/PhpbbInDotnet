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
    public class Translation
    {
        static readonly CultureInfo EN = new CultureInfo("en");

        private readonly string _name;
        private readonly Dictionary<string, Dictionary<string, string>> _translation;
        private readonly ILogger _logger;

        public Translation(string name, ILogger logger)
        {
            _name = name;
            _translation = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
            _logger = logger;
        }

        /// <summary>
        /// Get the translation in the given language for the given key
        /// </summary>
        /// <param name="language">Language to translate the key to</param>
        /// <param name="key">Key to translate</param>
        /// <param name="casing">Text casing</param>
        /// <returns>Translation of the given key in the given language if both exist and are properly defined. Empty string otherwise.</returns>
        public string this[string language, string key, Casing casing = Casing.None]
        {
            get
            {
                if (!_translation.ContainsKey(language))
                {
                    var value = GetDictionary(language);
                    if (value == null)
                    {
                        _logger.Warning($"Switching to default language '{Constants.DEFAULT_LANGUAGE}'...");
                        language = Constants.DEFAULT_LANGUAGE;
                        if (!_translation.ContainsKey(language))
                        {
                            _translation.Add(language, GetDictionary(language));
                        }
                    }
                    else
                    {
                        _translation.Add(language, value);
                    }
                }

                if (!_translation[language].TryGetValue(key, out var toReturn))
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
        }

        private string FirstUpper(string text)
            => char.ToUpper(text[0]) + text[1..].ToLower();

        private string TitleCase(string language, string text)
            => language.Equals("en", StringComparison.InvariantCultureIgnoreCase) ? EN.TextInfo.ToTitleCase(text) : FirstUpper(text);

        private Dictionary<string, string> GetDictionary(string language)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations", $"{_name}.{language}.json");
            if (!File.Exists(path))
            {
                _logger.Warning($"Potential missing language: '{language}' - file '{path}' does not exist.");
                return null;
            }

            var value = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            try
            {
                JsonConvert.PopulateObject(File.ReadAllText(path), value);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Failed to read file '{path}' and parse its contents.");
                return null;
            }

            return value;
        }
    }
}
