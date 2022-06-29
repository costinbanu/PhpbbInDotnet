using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PhpbbInDotnet.Domain.Utilities
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

        public static string HtmlSafeWhitespace(int count)
        {
            var options = new[] { " ", "&nbsp;" };
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append(options[i % 2]);
            }
            return sb.ToString();
        }

        public static string ReadableFileSize(long fileSizeInBytes)
        {
            var suf = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (fileSizeInBytes == 0)
            {
                return "0" + suf[0];
            }
            var bytes = Math.Abs(fileSizeInBytes);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return $"{(Math.Sign(fileSizeInBytes) * num).ToString("##.##", CultureInfo.InvariantCulture)} {suf[place]}";
        }
    }
}
