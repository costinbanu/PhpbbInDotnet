using Microsoft.AspNetCore.Mvc.Rendering;
using System;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class ViewContextExtensions
    {
        public static bool IsPage(this ViewContext viewContext, string page)
            => viewContext.RouteData.Values.TryGetValue("page", out var val) && 
                val?.ToString()?.Equals(page, StringComparison.OrdinalIgnoreCase) == true;
    }
}
