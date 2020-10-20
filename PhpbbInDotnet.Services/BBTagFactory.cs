using CodeKicker.BBCode.Core;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Web;

namespace PhpbbInDotnet.Services
{
    public class BBTagFactory
    {
        private readonly Dictionary<string, (BBTag Tag, BBTagSummary Summary)> _bbTags;

        public BBTagFactory(CommonUtils utils)
        {
            _bbTags = new Dictionary<string, (BBTag Tag, BBTagSummary Summary)>
            {
                ["b"] = (
                    Tag: new BBTag("b", "<b>", "</b>", 1), 
                    Summary: new BBTagSummary
                    {
                        ButtonText = "<b> B </b>",
                        OpenTag = "[b]",
                        CloseTag = "[/b]",
                        ShowOnPage = true,
                        ToolTip = "Text bold: [b]text[/b]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["i"] = (
                    Tag: new BBTag("i", "<span style=\"font-style:italic;\">", "</span>", 2),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "<i> i </i>",
                        OpenTag = "[i]",
                        CloseTag = "[/i]",
                        ShowOnPage = true,
                        ToolTip = "Text italic: [i]text[/i]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["u"] = (
                    Tag: new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>", 7),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "<u> u </u>",
                        OpenTag = "[u]",
                        CloseTag = "[/u]",
                        ShowOnPage = true,
                        ToolTip = "Text subliniat: [u]text[/u]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["strike"] = (
                    Tag: new BBTag("strike", "<span style=\"text-decoration: line-through\">", "</span>", 17),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "<strike>Text tăiat</strike>",
                        OpenTag = "[strike]",
                        CloseTag = "[/strike]",
                        ShowOnPage = true,
                        ToolTip = "Text tăiat: [strike]text[/strike]",
                        ShowWhenCollapsed = false
                    }
                ),

                ["color"] = (
                    Tag: new BBTag("color", "<span style=\"color:${code}\">", "</span>", 6, "", true,
                        new BBAttribute("code", "")),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Culoare text",
                        OpenTag = "[color]",
                        CloseTag = "[/color]",
                        ShowOnPage = true,
                        ToolTip = "Culoare font: [color=red]text[/color]  Sfat: de asemenea puteţi folosi culorile hexazecimale culoare=#FF0000",
                        ShowWhenCollapsed = false
                    }
                ),

                ["size"] = (
                    Tag: new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>", 5, "", true,
                        new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Mărime text",
                        OpenTag = "[size]",
                        CloseTag = "[/size]",
                        ShowOnPage = true,
                        ToolTip = "Mărime font: [size=85]text mic[/size]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["quote"] = (
                    Tag: new BBTag("quote", "<blockquote class=\"PostQuote\">${name}", "</blockquote>", 0, "", true,
                        new BBAttribute("name", "", (a) => string.IsNullOrWhiteSpace(a.AttributeValue) ? "" : $"<b>{HttpUtility.HtmlDecode(a.AttributeValue).Trim('"')}</b> a scris:<br/>", HtmlEncodingMode.UnsafeDontEncode))
                    {
                        GreedyAttributeProcessing = true
                    },
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Citează",
                        OpenTag = "[quote]",
                        CloseTag = "[/quote]",
                        ShowOnPage = true,
                        ToolTip = "Citează text: [quote]text[/quote]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["img"] = (
                    Tag: new BBTag("img", "<br/><img src=\"${content}\" onload=\"resizeImage(this)\" /><br/>", string.Empty, false, BBTagClosingStyle.RequiresClosingTag, content => UrlTransformer(content), false, 4, allowUrlProcessingAsText: false),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Imagine",
                        OpenTag = "[img]",
                        CloseTag = "[/img]",
                        ShowOnPage = true,
                        ToolTip = "Adaugă imagine: [img]http://cale_imagine[/img]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["url"] = (
                    Tag: new BBTag("url", "<a href=\"${href}\" target=\"_blank\">", "</a>", 3, "", false,
                        new BBAttribute("href", "", context => UrlTransformer(string.IsNullOrWhiteSpace(context?.AttributeValue) ? context.TagContent : context.AttributeValue), HtmlEncodingMode.UnsafeDontEncode)),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "URL",
                        OpenTag = "[url]",
                        CloseTag = "[/url]",
                        ShowOnPage = true,
                        ToolTip = "Adaugă URL: [url]http://url[/url] sau [url=http://url]text URL[/url]",
                        ShowWhenCollapsed = true
                    }
                ),

                ["code"] = (
                    Tag: new BBTag("code", "<span class=\"CodeBlock\">", "</span>", 8, allowUrlProcessingAsText: false),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Cod",
                        OpenTag = "[code]",
                        CloseTag = "[/code]",
                        ShowOnPage = true,
                        ToolTip = "Afişează cod: [code]cod[/code]",
                        ShowWhenCollapsed = false
                    }
                ),

                ["list"] = (
                    Tag: new BBTag("list", "<${attr}>", "</${attr}>", true, true, 9, "", true,
                        new BBAttribute("attr", "", a => string.IsNullOrWhiteSpace(a.AttributeValue) ? "ul style='list-style-type: circle'" : $"ol type=\"{a.AttributeValue}\"")),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Listă",
                        OpenTag = "[list]",
                        CloseTag = "[/list]",
                        ShowOnPage = true,
                        ToolTip = "Listă: [list][*]text[/list] sau listă ordonată: [list=1][*]text[/list]",
                        ShowWhenCollapsed = false
                    }
                ),

                ["*"] = (
                    Tag: new BBTag("*", "<li>", "</li>", true, BBTagClosingStyle.AutoCloseElement, x => x, true, 20),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "Element listă",
                        OpenTag = "[*]",
                        CloseTag = "",
                        ShowOnPage = true,
                        ToolTip = "Element listă: [*]text",
                        ShowWhenCollapsed = false
                    }
                ),
                
                ["attachment"] = (
                    Tag: new BBTag("attachment", "#{AttachmentFileName=${content}/AttachmentIndex=${num}}#", "", false, BBTagClosingStyle.AutoCloseElement, x => utils.HtmlCommentRegex.Replace(HttpUtility.HtmlDecode(x), string.Empty), 12, "", true,
                        new BBAttribute("num", "")),
                    Summary: new BBTagSummary
                    {
                        ShowOnPage = false
                    }
                ),

                //custom tags
                ["link"] = (
                    Tag: new BBTag("link", "<a href=\"${href}\">", "</a>", true, BBTagClosingStyle.RequiresClosingTag, x => utils.TransformSelfLinkToBetaLink(x), 18, "", false,
                        new BBAttribute("href", "", a => string.IsNullOrWhiteSpace(a?.AttributeValue) ? "${content}" : utils.TransformSelfLinkToBetaLink(a.AttributeValue))),
                    Summary: new BBTagSummary
                    {
                        ShowOnPage = false
                    }
                ),

                ["youtube"] = (
                    Tag: new BBTag("youtube", "<br /><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/${content}?html5=1\" frameborder=\"0\" allowfullscreen onload=\"resizeIFrame(this)\"></iframe><br />", string.Empty, false, false, 13, ""),
                    Summary: new BBTagSummary
                    {
                        ButtonText = "YouTube",
                        OpenTag = "[youtube]",
                        CloseTag = "[/youtube]",
                        ShowOnPage = true,
                        ToolTip = "Inserează în mesaj un film găzduit pe www.youtube.com: [youtube]COD_FILM[/youtube]",
                        ShowWhenCollapsed = true
                    }
                ),
            };
        }

        public (List<BBTag> BBTags, Dictionary<string, BBTagSummary> TagMap) GenerateCompleteTagListAndMap(IEnumerable<PhpbbBbcodes> dbCodes)
        {
            var tags = new List<BBTag>(_bbTags.Count);
            var tagMap = new Dictionary<string, BBTagSummary>(_bbTags.Count);
            foreach (var tag in _bbTags)
            {
                tags.Add(tag.Value.Tag);
                if (tag.Value.Summary.ShowOnPage)
                {
                    tagMap.TryAdd(tag.Key, tag.Value.Summary);
                }
            }
            foreach (var code in dbCodes)
            {
                tags.Add(new BBTag(code.BbcodeTag, code.BbcodeTpl, string.Empty, false, false, code.BbcodeId, "", true, new BBAttribute[0] { }));
                if (code.DisplayOnPosting.ToBool())
                {
                    tagMap.TryAdd(code.BbcodeTag, new BBTagSummary
                    {
                        ButtonText = code.BbcodeTag,
                        OpenTag = $"[{code.BbcodeTag}]",
                        CloseTag = $"[/{code.BbcodeTag}]",
                        ShowOnPage = code.DisplayOnPosting.ToBool(),
                        ToolTip = code.BbcodeHelpline,
                        ShowWhenCollapsed = false
                    });
                }
            }
            return (tags, tagMap);
        }

        private string UrlTransformer(string url)
        {
            if (!url.StartsWith("www", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException("Bad URL formatting");
            }
            else if (url.StartsWith("www", StringComparison.InvariantCultureIgnoreCase))
            {
                url = $"//{url}";
            }
            return url;
        }
    }
}
