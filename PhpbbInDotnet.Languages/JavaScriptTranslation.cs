using Serilog;

namespace PhpbbInDotnet.Languages
{
    public class JavaScriptTranslation : Translation
    {
        public JavaScriptTranslation(ILogger logger) : base("JavaScriptText", logger) { }

        protected override string FileExtension => "json";

        protected override bool ShouldCacheRawTranslation => true;

        public string this[string language]
            => GetRawTranslation(language);
    }
}
