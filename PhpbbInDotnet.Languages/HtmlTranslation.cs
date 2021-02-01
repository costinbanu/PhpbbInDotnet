using Serilog;

namespace PhpbbInDotnet.Languages
{
    public class HtmlTranslation : Translation
    {
        internal HtmlTranslation(string name, ILogger logger) : base(name, logger) { }

        protected override string FileExtension => "html";

        protected override bool ShouldCacheRawTranslation => true;

        public string this[string language]
            => GetRawTranslation(language);
    }
}
