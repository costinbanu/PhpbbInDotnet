using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface IBBCodeRenderingService
    {
        Dictionary<string, BBTagSummary> TagMap { get; }

        string BbCodeToHtml(string? bbCodeText, string? bbCodeUid);
        Task ProcessPost(PostDto post, bool renderAttachments, List<string>? toHighlight = null);
        List<string> SplitHighlightWords(string? search);
		string HighlightWords(string text, List<string> words);
	}
}