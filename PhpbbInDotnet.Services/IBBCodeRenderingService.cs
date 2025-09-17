using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface IBBCodeRenderingService
    {
        Task<Dictionary<string, BBTagSummary>> GetTagMap();
        Task<string> BbCodeToHtml(string? bbCodeText, string? bbCodeUid);
        Task ProcessPost(PostDto post, bool isPreview, List<string>? toHighlight = null);
        List<string> SplitHighlightWords(string? search);
		string HighlightWords(string text, List<string> words);
	}
}