using Newtonsoft.Json;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PhpbbInDotnet.Languages
{
    public class Translation
    {
        private readonly string _name;
        private readonly Dictionary<string, Dictionary<string, string>> _translation;

        public Translation(string name)
        {
            _name = name;
            _translation = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Get the translation in the given language for the given key
        /// </summary>
        /// <param name="language">Language to translate the key to</param>
        /// <param name="key">Key to translate</param>
        /// <returns>Translation of the given key in the given language if both exist and are properly defined. Empty string otherwise.</returns>
        public string this[string language, string key, Casing casing = Casing.AllLower]
        {
            get
            {
                if (!_translation.ContainsKey(language))
                {
                    var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations", $"{_name}.{language}.json");
                    if (!File.Exists(path))
                    {
                        return string.Empty;
                    }

                    var value = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    try
                    {
                        JsonConvert.PopulateObject(File.ReadAllText(path), value);
                    }
                    catch { }

                    _translation.Add(language, value);
                }

                var toReturn = _translation[language].TryGetValue(key, out var val) ? val : string.Empty;

                return casing switch
                {
                    Casing.AllLower => toReturn,
                    Casing.AllUpper => toReturn.ToUpper(),
                    Casing.FirstUpper => char.ToUpper(toReturn[0]) + toReturn[1..].ToLower(),
                    _ => toReturn
                };
            }
        }
    }
}
