using LazyCache;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;

namespace PhpbbInDotnet.Languages
{
    public class EnumTranslation : Translation
    {
        private readonly CommonUtils _utils;

        internal EnumTranslation(ILogger logger, IAppCache cache, CommonUtils utils) : base("Enums", logger, cache) 
        {
            _utils = utils;
        }

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => false;

        public string this[string language, Enum key, Casing casing = Casing.None]
            =>  GetFromDictionary(language, _utils.EnumString(key), casing, _utils.EnumString(key));
    }
}
