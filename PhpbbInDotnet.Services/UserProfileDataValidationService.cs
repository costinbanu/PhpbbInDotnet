using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PhpbbInDotnet.Services
{
    class UserProfileDataValidationService : IUserProfileDataValidationService
    {
        private readonly Regex USERNAME_REGEX = new(@"^[a-zA-Z0-9 \._-]+$", RegexOptions.Compiled);

        private readonly EmailAddressAttribute _emailValidator;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITranslationProvider _translationProvider;
        private readonly string _language;

        public UserProfileDataValidationService(IHttpContextAccessor httpContextAccessor, ITranslationProvider translationProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _translationProvider = translationProvider;
            _language = translationProvider.GetLanguage(ForumUserExpanded.GetValueOrDefault(httpContextAccessor.HttpContext!));
            _emailValidator = new EmailAddressAttribute();
        }

        public bool ValidateUsername(string name, string? value)
        {
            var toReturn = true;
            var modelState = (ModelStateDictionary)(_httpContextAccessor.HttpContext?.Items["ModelState"] ?? throw new Exception("Expected a ModelStateDictionary but found none"));

            if (string.IsNullOrWhiteSpace(value))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (value.Length < 2 || value.Length > 32)
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_USERNAME_LENGTH"]);
                toReturn = false;
            }
            else if (!USERNAME_REGEX.IsMatch(value))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_USERNAME_CHARS"]);
                toReturn = false;
            }
            else if (value.Equals(Constants.ANONYMOUS_USER_NAME, StringComparison.InvariantCultureIgnoreCase))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "ILLEGAL_USERNAME"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidateEmail(string name, string? value)
        {
            var toReturn = true;
            var modelState = (ModelStateDictionary)(_httpContextAccessor.HttpContext?.Items["ModelState"] ?? throw new Exception("Expected a ModelStateDictionary but found none"));

            if (string.IsNullOrWhiteSpace(value))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (!_emailValidator.IsValid(value))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "INVALID_EMAIL_ADDRESS"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidatePassword(string name, string? value)
        {
            var toReturn = true;
            var modelState = (ModelStateDictionary)(_httpContextAccessor.HttpContext?.Items["ModelState"] ?? throw new Exception("Expected a ModelStateDictionary but found none"));
            
            if (string.IsNullOrWhiteSpace(value))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (value.Length < 8 || value.Length > 256)
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_PASSWORD_LENGTH"]);
                toReturn = false;
            }
            else if (!IsPasswordValid(value))
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_PASSWORD_CHARS"]);
                toReturn = false;
            }

            return toReturn;

            static bool IsPasswordValid(string password)
            {
                bool foundLetter = false, foundDigit = false;
                foreach (var c in password)
                {
                    if (char.IsLetter(c))
                    {
                        foundLetter = true;
                    }
                    else if(char.IsDigit(c))
                    {
                        foundDigit = true;
                    }
                }
                return foundLetter && foundDigit;
            }
        }

        public bool ValidateSecondPassword(string secondName, string? secondValue, string? firstValue)
        {
            var toReturn = true;
            var modelState = (ModelStateDictionary)(_httpContextAccessor.HttpContext?.Items["ModelState"] ?? throw new Exception("Expected a ModelStateDictionary but found none"));
            
            if (string.IsNullOrWhiteSpace(secondValue))
            {
                modelState.AddModelError(secondName, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (firstValue != secondValue)
            {
                modelState.AddModelError(secondName, _translationProvider.Errors[_language, "PASSWORD_MISMATCH"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidateTermsAgreement(string name, bool value)
        {
            var modelState = (ModelStateDictionary)(_httpContextAccessor.HttpContext?.Items["ModelState"] ?? throw new Exception("Expected a ModelStateDictionary but found none"));
            if (!value)
            {
                modelState.AddModelError(name, _translationProvider.Errors[_language, "MUST_AGREE_WITH_TERMS"]);
                return false;
            }
            return true;
        }
    }
}
