using LazyCache;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;

namespace PhpbbInDotnet.Languages
{
    public class EnumTranslation : Translation
    {
        internal EnumTranslation(ILogger logger, IAppCache cache) : base("Enums", logger, cache) { }

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => false;

        public string this[string language, Enum key, Casing casing = Casing.None]
        {
            get
            {
                var stringKey = $"{key.GetType().Name}.{key}";
                return GetFromDictionary(language, stringKey, casing, stringKey);
            }
        }
    }
}
