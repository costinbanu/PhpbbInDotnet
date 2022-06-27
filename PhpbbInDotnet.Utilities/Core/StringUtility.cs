using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PhpbbInDotnet.Utilities.Core
{
    public static class StringUtility
    {
        public static readonly Regex HtmlCommentRegex = new ("(<!--.*?-->)|(&lt;!--.*?--&gt;)", RegexOptions.Compiled | RegexOptions.Singleline, Constants.REGEX_TIMEOUT);

        public static string CleanString(string? input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().ToLower().Normalize(NormalizationForm.FormC);
        }
    }
}
