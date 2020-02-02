using System.Collections.Generic;

namespace Serverless.Forum.Utilities
{
    public static class Constants
    {
        public static readonly string FORUM_NAME = "Forumul Metrou Ușor";

        public static readonly string FORUM_BASE_URL = "http://localhost:54356" ;//"https://forum.metrouusor.com";

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
    }
}
