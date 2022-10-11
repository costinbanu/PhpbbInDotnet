using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class ViewDataDictionaryExtensions
    {
        public static T? ValueOrDefault<T>(this ViewDataDictionary viewData, string key)
            => viewData.TryGetValue(key, out var raw) && raw is T val ? val : default;
    }
}
