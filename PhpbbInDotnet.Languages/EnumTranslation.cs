using LazyCache;
using PhpbbInDotnet.Domain;
using Serilog;
using System;

namespace PhpbbInDotnet.Languages
{
    public class EnumTranslation : Translation
    {
        private readonly ICommonUtils _utils;

        internal EnumTranslation(ILogger logger, IAppCache cache, ICommonUtils utils) : base("Enums", logger, cache) 
        {
            _utils = utils;
        }

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => false;

        public string this[string language, Enum key, Casing casing = Casing.None]
            =>  GetFromDictionary(language, _utils.EnumString(key), casing, _utils.EnumString(key));
    }
}
