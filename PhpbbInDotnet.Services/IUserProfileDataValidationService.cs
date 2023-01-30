namespace PhpbbInDotnet.Services
{
    public interface IUserProfileDataValidationService
    {
        bool ValidateEmail(string name, string? value);
        bool ValidatePassword(string name, string? value);
        bool ValidateSecondPassword(string secondName, string? secondValue, string? firstValue);
        bool ValidateTermsAgreement(string name, bool value);
        bool ValidateUsername(string name, string? value);
    }
}