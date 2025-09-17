using CodeKicker.BBCode.Core;
using Dapper;
using LazyCache;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    partial class BBCodeRenderingService
    {
        private (List<BBTag> BBTagSummary, Dictionary<string, BBTagSummary> BBTagMap)? _bbTags;
        private async Task<(List<BBTag> BBTags, Dictionary<string, BBTagSummary> TagMap)> GetBBTags()
        {
            if (_bbTags is null)
            {
                var lang = _translationProvider.GetLanguage();
                _bbTags = await _cache.GetOrAddAsync<(List<BBTag> BBTags, Dictionary<string, BBTagSummary>)>(
                    key: _writingService.GetBbCodesCacheKey(lang),
                    addItemFactory: async () =>
                    {
                        var tagList = (await _sqlExecuter.QueryAsync<PhpbbBbcodes>("SELECT * FROM phpbb_bbcodes")).AsList();
                        var maxId = tagList.Max(t => t.BbcodeId);
                        var tagsCache = new Dictionary<string, (BBTag Tag, BBTagSummary Summary)>
                        {
                            ["b"] = (
                                Tag: new BBTag("b", "<b>", "</b>", maxId + 1),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[b]",
                                    CloseTag = "[/b]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),

                            ["i"] = (
                                Tag: new BBTag("i", "<span style=\"font-style:italic;\">", "</span>", maxId + 2),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[i]",
                                    CloseTag = "[/i]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),

                            ["u"] = (
                                Tag: new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>", maxId + 3),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[u]",
                                    CloseTag = "[/u]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),

                            ["color"] = (
                                Tag: new BBTag("color", "<span style=\"color:${code}\">", "</span>", maxId + 4, attributes: new[] { new BBAttribute("code", "") }),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[color]",
                                    CloseTag = "[/color]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = false
                                }
                            ),

                            ["size"] = (
                                Tag: new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>", maxId + 5, attributes: new[]
                                {
                                new BBAttribute("fsize", "", a => decimal.TryParse(a?.AttributeValue, out var val) ? FormattableString.Invariant($"{val / 100m:#.##}em") : "1em")
                                }),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[size]",
                                    CloseTag = "[/size]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),

                            ["quote"] = (
                                Tag: new BBTag("quote", "<blockquote class=\"PostQuote\">${name}", "</blockquote>", maxId + 6, greedyAttributeProcessing: true, attributes: new[]
                                {
                                new BBAttribute(
                                    id: "name",
                                    name: "",
                                    contentTransformer: a =>
                                    {
                                        if (string.IsNullOrWhiteSpace(a.AttributeValue))
                                        {
                                            return string.Empty;
                                        }
                                        var match = _quoteAttributeRegex.Match(HttpUtility.HtmlDecode(a.AttributeValue));
                                        if (!match.Success || match.Groups.Count != 3)
                                        {
                                            return string.Empty;
                                        }
                                        var toReturn = match.Groups[1].Success ? string.Format(_translationProvider.BasicText[lang, "WROTE_FORMAT"], match.Groups[1].Value.Trim('"')) : string.Empty;
                                        var stringPostId = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
                                        if (!string.IsNullOrWhiteSpace(toReturn))
                                        {
                                            if (int.TryParse(stringPostId, out var postId))
                                            {
                                                toReturn += $" <a href=\"{ForumLinkUtility.GetRelativeUrlToPost(postId)}\" target=\"_blank\">{_translationProvider.BasicText[lang, "HERE"]}</a>:<br />";
                                            }
                                            else
                                            {
                                                toReturn += ":<br />";
                                            }
                                        }
                                        return toReturn;
                                    },
                                    htmlEncodingMode: HtmlEncodingMode.UnsafeDontEncode)
                                }),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[quote]",
                                    CloseTag = "[/quote]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),

                            ["img"] = (
                                Tag: new BBTag(
                                    name: "img",
                                    openTagTemplate: $"<br/><img src=\"${{content}}\" class=\"ImageSize\" onload=\"openImageInNewWindowOnClick(this)\" /><br/>",
                                    closeTagTemplate: string.Empty,
                                    id: maxId + 7,
                                    autoRenderContent: false,
                                    contentTransformer: content => UrlTransformer(content),
                                    allowUrlProcessingAsText: false,
                                    allowChildren: false),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[img]",
                                    CloseTag = "[/img]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }),

                            ["url"] = (
                                Tag: new BBTag("url", "<a href=\"${href}\" target=\"_blank\">", "</a>", maxId + 8, allowUrlProcessingAsText: false, attributes: new[]
                                {
                                new BBAttribute("href", "", context => UrlTransformer(string.IsNullOrWhiteSpace(context.AttributeValue) ? context.TagContent : context.AttributeValue), HtmlEncodingMode.UnsafeDontEncode)
                                }),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[url]",
                                    CloseTag = "[/url]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),

                            ["code"] = (
                                Tag: new BBTag("code", "<span class=\"CodeBlock\">", "</span>", maxId + 9, allowUrlProcessingAsText: false, allowChildren: false),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[code]",
                                    CloseTag = "[/code]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = false
                                }
                            ),

                            ["list"] = (
                                Tag: new BBTag("list", "<${attr}>", "</${attr}>", maxId + 10, attributes: new[]
                                {
                                new BBAttribute("attr", "", a => string.IsNullOrWhiteSpace(a.AttributeValue) ? "ul style='list-style-type: circle'" : $"ol type=\"{a.AttributeValue}\"")
                                }),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[list]",
                                    CloseTag = "[/list]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = false
                                }
                            ),

                            ["*"] = (
                                Tag: new BBTag("*", "<li>", "</li>", maxId + 11, tagClosingStyle: BBTagClosingStyle.AutoCloseElement, enableIterationElementBehavior: true),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[*]",
                                    CloseTag = "",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = false
                                }
                            ),

                            ["attachment"] = (
                                Tag: new BBTag(
                                    name: "attachment",
                                    openTagTemplate: "#{AttachmentFileName=${content}/AttachmentIndex=${num}}#",
                                    closeTagTemplate: "",
                                    id: maxId + 12,
                                    autoRenderContent: false,
                                    tagClosingStyle: BBTagClosingStyle.AutoCloseElement,
                                    contentTransformer: FileNameTransformer,
                                    allowChildren: false,
                                    attributes: new[] { new BBAttribute("num", "") }),
                                Summary: new BBTagSummary
                                {
                                    ShowOnPage = false
                                }
                            ),

                            ["quoted-attachment"] = (
                                Tag: new BBTag(
                                    name: "quoted-attachment",
                                    openTagTemplate: "#{QuotedAttachmentFileName=${content}/QuotedAttachmentIndexAndPostId=${num}}#",
                                    closeTagTemplate: "",
                                    id: maxId + 13,
                                    autoRenderContent: false,
                                    tagClosingStyle: BBTagClosingStyle.AutoCloseElement,
                                    contentTransformer: FileNameTransformer,
                                    allowChildren: false,
                                    attributes: new[] { new BBAttribute("num", "") }),
                                Summary: new BBTagSummary
                                {
                                    ShowOnPage = false
                                }
                            ),

                            ["youtube"] = (
                                Tag: new BBTag(
                                    name: "youtube",
                                    openTagTemplate: "<br /><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/${content}?html5=1\" frameborder=\"0\" allowfullscreen onload=\"resizeIFrame(this)\"></iframe><br />",
                                    closeTagTemplate: string.Empty,
                                    id: maxId + 14,
                                    autoRenderContent: false,
                                    tagClosingStyle: BBTagClosingStyle.AutoCloseElement),
                                Summary: new BBTagSummary
                                {
                                    OpenTag = "[youtube]",
                                    CloseTag = "[/youtube]",
                                    ShowOnPage = true,
                                    ShowWhenCollapsed = true
                                }
                            ),
                        };

                        var tags = new List<BBTag>(tagsCache.Count);
                        var tagMap = new Dictionary<string, BBTagSummary>(tagsCache.Count);
                        foreach (var tag in tagsCache)
                        {
                            tags.Add(tag.Value.Tag);
                            if (tag.Value.Summary.ShowOnPage)
                            {
                                tagMap.TryAdd(tag.Key, tag.Value.Summary);
                            }
                        }
                        foreach (var code in tagList)
                        {
                            var attributes = _attrRegex.Matches(code.BbcodeTpl).Where(m => m.Success && !m.Value.Equals("${content}", StringComparison.InvariantCultureIgnoreCase)).Select(m => new BBAttribute(m.Value.Trim("${}".ToCharArray()), "", ctx => ctx.AttributeValue));
                            tags.Add(new BBTag(code.BbcodeTag, code.BbcodeTpl, string.Empty, code.BbcodeId, autoRenderContent: false, bbcodeUid: "", tagClosingStyle: BBTagClosingStyle.AutoCloseElement, attributes: attributes));

                            if (code.DisplayOnPosting.ToBool())
                            {
                                tagMap.TryAdd(code.BbcodeTag, new BBTagSummary
                                {
                                    OpenTag = $"[{code.BbcodeTag}]",
                                    CloseTag = $"[/{code.BbcodeTag}]",
                                    ShowOnPage = code.DisplayOnPosting.ToBool(),
                                    ShowWhenCollapsed = false
                                });
                            }
                        }
                        return (tags, tagMap);
                    },
                    expires: DateTimeOffset.UtcNow.AddHours(4));
            }
            return _bbTags.Value;
        }

        private BBCodeParser? _parser;
        private async Task<BBCodeParser> GetParser()
        {
            if (_parser is null)
            {
                var (tags, _) = await GetBBTags();
                _parser = new BBCodeParser(tags);
            }
            return _parser;
        }

        public async Task<Dictionary<string, BBTagSummary>> GetTagMap()
        {
            var (_, map) = await GetBBTags();
            return map;
        }
    }
}
