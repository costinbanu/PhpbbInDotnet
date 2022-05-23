using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IBBCodeRenderingService
    {
        Dictionary<string, BBTagSummary> TagMap { get; }

        string BbCodeToHtml(string? bbCodeText, string? bbCodeUid);
        Task ProcessPost(PostDto post, PageContext pageContext, HttpContext httpContext, bool renderAttachments, string? toHighlight = null);
    }
}