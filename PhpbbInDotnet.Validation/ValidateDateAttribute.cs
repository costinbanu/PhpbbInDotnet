using System;
using System.ComponentModel.DataAnnotations;

namespace PhpbbInDotnet.Validation
{
    public class ValidateDateAttribute : RequiredAttribute
    {
        public override bool IsValid(object? value)
            => string.IsNullOrWhiteSpace(value?.ToString()) || DateTime.TryParse(value?.ToString(), out var _);
    }
}
