using System;
using System.ComponentModel.DataAnnotations;

namespace PhpbbInDotnet.Forum.Utilities
{
    public class ValidateDateAttribute : RequiredAttribute
    {
        public override bool IsValid(object value)
            => string.IsNullOrWhiteSpace(value?.ToString()) || DateTime.TryParse(value?.ToString(), out var _);
    }
}
