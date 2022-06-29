using System.Collections.Generic;
using System.Linq;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<(T Item, int Index)> Indexed<T>(this IEnumerable<T> source, int startIndex = 0)
        {
            var i = startIndex;
            foreach (var item in source)
            {
                yield return (item, i++);
            }
        }

        public static IEnumerable<T?> DefaultIfNullOrEmpty<T>(this IEnumerable<T>? source)
            => source.EmptyIfNull().DefaultIfEmpty();

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source)
            => source ?? Enumerable.Empty<T>();
    }
}
