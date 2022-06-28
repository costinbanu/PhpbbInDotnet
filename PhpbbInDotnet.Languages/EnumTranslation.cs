using LazyCache;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using Serilog;
using System;

namespace PhpbbInDotnet.Languages
{
    public class EnumTranslation : Translation
    {
        internal EnumTranslation(ILogger logger, IAppCache cache) : base("Enums", logger, cache) 
        {
        }

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => false;

        public string this[string language, Enum key, Casing casing = Casing.None]
            =>  GetFromDictionary(language, EnumUtility.ExpandEnum(key), casing, EnumUtility.ExpandEnum(key));
    }
}
