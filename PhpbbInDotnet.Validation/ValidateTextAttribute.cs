using PhpbbInDotnet.Languages;
using System;
using System.ComponentModel.DataAnnotations;

namespace PhpbbInDotnet.Validation
{
    public class ValidateTextAttribute : RequiredAttribute
    {
        public int MinLength { get; set; } = int.MinValue;

        public int MaxLength { get; set; } = int.MaxValue;

        public string TooShortKey { get; set; } = string.Empty;

        public string TooLongKey { get; set; } = string.Empty;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {

            if (value is string stringValue)
            {
                var length = stringValue.Trim().Length;
                if (length < MinLength)
                {
                    return new ValidationResult(TranslatedMessageOrDefault(TooShortKey));
                }
                if (length > MaxLength)
                {
                    return new ValidationResult(TranslatedMessageOrDefault(TooLongKey));
                }
                return ValidationResult.Success;
            }
            return new ValidationResult(TranslatedMessageOrDefault(TooShortKey));

            string TranslatedMessageOrDefault(string key)
            {
                if (validationContext.GetService(typeof(ITranslationProvider)) is ITranslationProvider translationProvider)
                {
                    return translationProvider.Errors[translationProvider.GetLanguage(), key];
                }
                return "The value is invalid";
            }
        }
    }
}
