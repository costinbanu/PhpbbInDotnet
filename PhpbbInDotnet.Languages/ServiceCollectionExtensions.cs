using Microsoft.Extensions.DependencyInjection;

namespace PhpbbInDotnet.Languages
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLanguageSupport(this IServiceCollection services)
            => services.AddScoped<ITranslationProvider, TranslationProvider>();
    }
}
