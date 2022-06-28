using PhpbbInDotnet.Domain;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCommonUtils(this IServiceCollection services)
            => services.AddSingleton<ICommonUtils, CommonUtils>();
    }
}
