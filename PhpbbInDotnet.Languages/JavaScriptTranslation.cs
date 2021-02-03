using LazyCache;
using Serilog;

namespace PhpbbInDotnet.Languages
{
    public class JavaScriptTranslation : Translation
    {
        internal JavaScriptTranslation(ILogger logger, IAppCache cache) : base("JavaScriptText", logger, cache) { }

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => true;

        public string this[string language]
            => GetRawTranslation(language);
    }
}
