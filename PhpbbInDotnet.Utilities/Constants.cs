using System.Collections.Generic;

namespace PhpbbInDotnet.Utilities
{
    public static class Constants
    {
        public static readonly string REPLY = "Re: ";

        public static readonly string[] BBCODES = new[]
        {
            "[b]",
            "[/b]",
            "[i]",
            "[/i]",
            "[u]",
            "[/u]",
            "[quote]",
            "[/quote]",
            "[code]",
            "[/code]",
            "[list]",
            "[/list]",
            "[list=]",
            "[/list]",
            "[img]",
            "[/img]",
            "[url]",
            "[/url]",
            "[flash=]",
            "[/flash]",
            "[size=]",
            "[/size]"
        };

        public static readonly Dictionary<string, string> BBCODE_HELPLINES = new Dictionary<string, string>
        {
            {"b", "Text bold: [b]text[/b]"},
            {"i", "Text italic: [i]text[/i]"},
            {"u", "Text subliniat: [u]text[/u]"},
            {"q", "Citează text: [quote]text[/quote]"},
            {"c", "Afişează cod: [code]cod[/code]"},
            {"l", "Listă: [list]text[/list]"},
            {"o", "Listă ordonată: [list=]text[/list]"},
            {"p", "Adaugă imagine: [img]http://cale_imagine[/img]"},
            {"w", "Adaugă URL: [url]http://url[/url] sau [url=http://url]text URL[/url]"},
            {"a", "Încarcă fişier ataşat în line: [attachment=]numefişier.ext[/attachment]"},
            {"s", "Culoare font: [color=red]text[/color]  Sfat: de asemenea puteţi folosi culorile hexazecimale culoare=#FF0000"},
            {"f", "Mărime font: [size=85]text mic[/size]"},
            {"e", "Listă: Adaugă element de listă"},
            {"d", "Flash: [flash=width,height]http://url[/flash]"}
        };

        public static readonly string SMILEY_PATH = "./images/smilies";

        public static readonly int ANONYMOUS_USER_ID = 1;

        public static readonly int NO_PM_ROLE = 8;

        public static readonly int OTHER_REPORT_REASON_ID = 4;

        public static readonly int ACCESS_TO_FORUM_DENIED_ROLE = 16;

        public static readonly int ADMIN_GROUP_ID = 5;

        public static readonly int PAGE_SIZE_INCREMENT = 7;

        public static readonly int DEFAULT_PAGE_SIZE = PAGE_SIZE_INCREMENT * 2;
    }
}
