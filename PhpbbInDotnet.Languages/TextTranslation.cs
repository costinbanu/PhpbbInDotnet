﻿using LazyCache;
using PhpbbInDotnet.Utilities;
using Serilog;

namespace PhpbbInDotnet.Languages
{
    public class TextTranslation : Translation
    {
        internal TextTranslation(string name, ILogger logger, IAppCache cache) : base(name, logger, cache) { }

        public string this[string language, string key, Casing casing = Casing.None] 
            => GetFromDictionary(language, key, casing);

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => false;
    }
}
