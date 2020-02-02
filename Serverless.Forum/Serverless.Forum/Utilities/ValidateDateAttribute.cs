using System;
using System.ComponentModel.DataAnnotations;

namespace Serverless.Forum.Utilities
{
    public class ValidateDateAttribute : RequiredAttribute
    {
        public override bool IsValid(object value)
        {
            return DateTime.TryParse(value?.ToString(), out var _);
        }
    }
}
