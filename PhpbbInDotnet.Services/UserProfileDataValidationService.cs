using Microsoft.AspNetCore.Mvc.Infrastructure;
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
        private readonly Regex PASSWORD_REGEX = new(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$", RegexOptions.Compiled);

        private readonly EmailAddressAttribute _emailValidator;
        private readonly ModelStateDictionary _modelState;
        private readonly ITranslationProvider _translationProvider;
        private readonly string _language;

        public UserProfileDataValidationService(IActionContextAccessor actionContextAccessor, ITranslationProvider translationProvider)
        {
            _modelState = actionContextAccessor.ActionContext?.ModelState ?? throw new ArgumentNullException(nameof(actionContextAccessor.ActionContext));
            _translationProvider = translationProvider;
            _language = translationProvider.GetLanguage(ForumUserExpanded.GetValueOrDefault(actionContextAccessor.ActionContext.HttpContext));
            _emailValidator = new EmailAddressAttribute();
        }

        public bool ValidateUsername(string name, string? value)
        {
            var toReturn = true;

            if (string.IsNullOrWhiteSpace(value))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (value.Length < 2 || value.Length > 32)
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_USERNAME_LENGTH"]);
                toReturn = false;
            }
            else if (!USERNAME_REGEX.IsMatch(value))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_USERNAME_CHARS"]);
                toReturn = false;
            }
            else if (value.Equals(Constants.ANONYMOUS_USER_NAME, System.StringComparison.InvariantCultureIgnoreCase))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "ILLEGAL_USERNAME"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidateEmail(string name, string? value)
        {
            var toReturn = true;

            if (string.IsNullOrWhiteSpace(value))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (!_emailValidator.IsValid(value))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "INVALID_EMAIL_ADDRESS"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidatePassword(string name, string? value)
        {
            var toReturn = true;

            if (string.IsNullOrWhiteSpace(value))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (value.Length < 8 || value.Length > 256)
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_PASSWORD_LENGTH"]);
                toReturn = false;
            }
            else if (!PASSWORD_REGEX.IsMatch(value))
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "BAD_PASSWORD_CHARS"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidateSecondPassword(string secondName, string? secondValue, string? firstValue)
        {
            var toReturn = true;

            if (string.IsNullOrWhiteSpace(secondValue))
            {
                _modelState.AddModelError(secondName, _translationProvider.Errors[_language, "MISSING_REQUIRED_FIELD"]);
                toReturn = false;
            }
            else if (firstValue != secondValue)
            {
                _modelState.AddModelError(secondName, _translationProvider.Errors[_language, "PASSWORD_MISMATCH"]);
                toReturn = false;
            }

            return toReturn;
        }

        public bool ValidateTermsAgreement(string name, bool value)
        {
            if (!value)
            {
                _modelState.AddModelError(name, _translationProvider.Errors[_language, "MUST_AGREE_WITH_TERMS"]);
                return false;
            }
            return true;
        }
    }
}
