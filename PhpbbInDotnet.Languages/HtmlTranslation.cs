using LazyCache;
using Serilog;

namespace PhpbbInDotnet.Languages
{
    public class HtmlTranslation : Translation
    {
        internal HtmlTranslation(string name, ILogger logger, IAppCache cache) : base(name, logger, cache) { }

        protected override string FileExtension => "html";

        protected override bool ShouldCacheRawTranslation => true;

        public string this[string language]
            => GetRawTranslation(language);
    }
}
