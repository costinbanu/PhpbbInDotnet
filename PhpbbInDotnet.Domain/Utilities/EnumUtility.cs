using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class EnumUtility
    {
        public const string DefaultValue = "dummyValue";

        public static string ExpandEnum(Enum @enum)
            => $"{@enum.GetType().Name}.{@enum}";

        public static List<SelectListItem> EnumToDropDownList<T>(T? selectedItem, Func<T, string>? textTransform = null, Func<T, string>? valueTransform = null, string? defaultText = null, Func<T, bool>? valueFilter = null)
            where T : struct, Enum
        {
            textTransform ??= x => Enum.GetName(x)!;
            valueTransform ??= x => Enum.GetName(x)!;
            valueFilter ??= x => true;
            var toReturn = Enum.GetValues<T>().Where(valueFilter).Select(
                val => new SelectListItem(textTransform(val), valueTransform(val), selectedItem.HasValue && Enum.GetName(selectedItem.Value) == Enum.GetName(val))
            ).ToList();
            if (!selectedItem.HasValue && !string.IsNullOrWhiteSpace(defaultText))
            {
                toReturn.Insert(0, new SelectListItem(defaultText, DefaultValue, true, true));
            }
            return toReturn;
        }
    }
}
