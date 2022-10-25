using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        public static bool IsMimeTypeInline(string? mimeType)
            => IsImageMimeType(mimeType) /*||
                mimeType.StartsWith("video", StringComparison.InvariantCultureIgnoreCase) ||
                mimeType.EndsWith("pdf", StringComparison.InvariantCultureIgnoreCase)*/;

        public static bool IsImageMimeType(string? mimeType)
            => mimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

        public static HashSet<int> ToIntHashSet(string? list)
        {
            if (string.IsNullOrWhiteSpace(list))
            {
                return new HashSet<int>();
            }
            var items = list.Split(',');
            var toReturn = new HashSet<int>(items.Length);
            foreach (var item in items)
            {
                try
                {
                    if (int.TryParse(item.Trim(), out var val))
                    {
                        toReturn.Add(val);
                    }
                }
                catch { }
            };
            return toReturn;
        }

        public static string ReplaceHtmlDiacritics(string input)
        {
            var matches = CHAR_CODES_REGEX.Matches(input).AsEnumerable().Where(m => m.Success);
            foreach (var match in matches)
            {
                if (CHAR_CODES.TryGetValue(match.Value, out var replacement))
                {
                    input = input.Replace(match.Value, replacement);
                }
            }

            return input;
        }

        private static readonly Regex CHAR_CODES_REGEX = new("&#[0-9]{3};", RegexOptions.Compiled | RegexOptions.CultureInvariant, Constants.REGEX_TIMEOUT);

        private static readonly Dictionary<string, string> CHAR_CODES = new()
        {
            ["&#192;"] = "À",
            ["&#193;"] = "Á",
            ["&#194;"] = "Â",
            ["&#195;"] = "Ã",
            ["&#196;"] = "Ä",
            ["&#197;"] = "Å",
            ["&#198;"] = "Æ",
            ["&#199;"] = "Ç",
            ["&#200;"] = "È",
            ["&#201;"] = "É",
            ["&#202;"] = "Ê",
            ["&#203;"] = "Ë",
            ["&#204;"] = "Ì",
            ["&#205;"] = "Í",
            ["&#206;"] = "Î",
            ["&#207;"] = "Ï",
            ["&#208;"] = "Ð",
            ["&#209;"] = "Ñ",
            ["&#210;"] = "Ò",
            ["&#211;"] = "Ó",
            ["&#212;"] = "Ô",
            ["&#213;"] = "Õ",
            ["&#214;"] = "Ö",
            ["&#216;"] = "Ø",
            ["&#217;"] = "Ù",
            ["&#218;"] = "Ú",
            ["&#219;"] = "Û",
            ["&#220;"] = "Ü",
            ["&#221;"] = "Ý",
            ["&#222;"] = "Þ",
            ["&#223;"] = "ß",
            ["&#224;"] = "à",
            ["&#225;"] = "á",
            ["&#226;"] = "â",
            ["&#227;"] = "ã",
            ["&#228;"] = "ä",
            ["&#229;"] = "å",
            ["&#230;"] = "æ",
            ["&#231;"] = "ç",
            ["&#232;"] = "è",
            ["&#233;"] = "é",
            ["&#234;"] = "ê",
            ["&#235;"] = "ë",
            ["&#236;"] = "ì",
            ["&#237;"] = "í",
            ["&#238;"] = "î",
            ["&#239;"] = "ï",
            ["&#240;"] = "ð",
            ["&#241;"] = "ñ",
            ["&#242;"] = "ò",
            ["&#243;"] = "ó",
            ["&#244;"] = "ô",
            ["&#245;"] = "õ",
            ["&#246;"] = "ö",
            ["&#248;"] = "ø",
            ["&#249;"] = "ù",
            ["&#250;"] = "ú",
            ["&#251;"] = "û",
            ["&#252;"] = "ü",
            ["&#253;"] = "ý",
            ["&#254;"] = "þ",
            ["&#255;"] = "ÿ"
        };

        private static readonly Dictionary<string, string> CHAR_NAMES = new()
        {
            ["&Agrave;"] = "À",
            ["&Aacute;"] = "Á",
            ["&Acirc;"] = "Â",
            ["&Atilde;"] = "Ã",
            ["&Auml;"] = "Ä",
            ["&Aring;"] = "Å",
            ["&AElig;"] = "Æ",
            ["&Ccedil;"] = "Ç",
            ["&Egrave;"] = "È",
            ["&Eacute;"] = "É",
            ["&Ecirc;"] = "Ê",
            ["&Euml;"] = "Ë",
            ["&Igrave;"] = "Ì",
            ["&Iacute;"] = "Í",
            ["&Icirc;"] = "Î",
            ["&Iuml;"] = "Ï",
            ["&ETH;"] = "Ð",
            ["&Ntilde;"] = "Ñ",
            ["&Ograve;"] = "Ò",
            ["&Oacute;"] = "Ó",
            ["&Ocirc;"] = "Ô",
            ["&Otilde;"] = "Õ",
            ["&Ouml;"] = "Ö",
            ["&Oslash;"] = "Ø",
            ["&Ugrave;"] = "Ù",
            ["&Uacute;"] = "Ú",
            ["&Ucirc;"] = "Û",
            ["&Uuml;"] = "Ü",
            ["&Yacute;"] = "Ý",
            ["&THORN;"] = "Þ",
            ["&szlig;"] = "ß",
            ["&agrave;"] = "à",
            ["&aacute;"] = "á",
            ["&acirc;"] = "â",
            ["&atilde;"] = "ã",
            ["&auml;"] = "ä",
            ["&aring;"] = "å",
            ["&aelig;"] = "æ",
            ["&ccedil;"] = "ç",
            ["&egrave;"] = "è",
            ["&eacute;"] = "é",
            ["&ecirc;"] = "ê",
            ["&euml;"] = "ë",
            ["&igrave;"] = "ì",
            ["&iacute;"] = "í",
            ["&icirc;"] = "î",
            ["&iuml;"] = "ï",
            ["&eth;"] = "ð",
            ["&ntilde;"] = "ñ",
            ["&ograve;"] = "ò",
            ["&oacute;"] = "ó",
            ["&ocirc;"] = "ô",
            ["&otilde;"] = "õ",
            ["&ouml;"] = "ö",
            ["&oslash;"] = "ø",
            ["&ugrave;"] = "ù",
            ["&uacute;"] = "ú",
            ["&ucirc;"] = "û",
            ["&uuml;"] = "ü",
            ["&yacute;"] = "ý",
            ["&thorn;"] = "þ",
            ["&yuml;"] = "ÿ"
        };
    }
}
