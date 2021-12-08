using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System.Linq;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _PollPartialModel : PageModel
    {
        public AuthenticatedUserExpanded? CurrentUser { get; set; }

        public PollDto? Poll { get; set; }

        public int TopicId { get; set; }

        public int PageNum { get; set; }

        public string? QueryString { get; set; }

        public bool IsCurrentUserMod { get; set; }

        public bool IsCurrentUserAdmin { get; set; }

        public bool IsPreview { get; set; }

        private bool? _canVoteNow;

        public bool CanVoteNow => _canVoteNow ??= !(
            (CurrentUser?.UserId ?? Constants.ANONYMOUS_USER_ID) <= 1 ||
            (Poll?.PollEnded ?? false) ||
            (
                (Poll?.PollOptions?.Any(o => o.PollOptionVoters?.Any(v => v.UserId == (CurrentUser?.UserId ?? Constants.ANONYMOUS_USER_ID)) ?? false) ?? false) && 
                !(Poll?.VoteCanBeChanged ?? false)
            )
        );

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
    }
}
