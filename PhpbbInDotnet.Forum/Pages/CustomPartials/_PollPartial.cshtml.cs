using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;
using System.Linq;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _PollPartialModel : PageModel
    {
        public ForumUserExpanded CurrentUser { get; }

        public PollDto Poll { get; }

        public int TopicId { get; }

        public int PageNum { get; }

        public string? QueryString { get; }

        public bool IsCurrentUserMod { get; }

        public bool IsCurrentUserAdmin { get; }

        public bool IsPreview { get; }

        public bool CanVoteNow { get; }

        public string Language => CurrentUser?.Language ?? Constants.DEFAULT_LANGUAGE;

        public _PollPartialModel(ForumUserExpanded currentUser, PollDto poll, bool isPreview, int topicId = 0, int pageNum = 0, string? queryString = null, bool isCurrentUserMod = false, bool isCurrentUserAdmin = false, bool isTopicLocked = false)
        {
            CurrentUser = currentUser;
            Poll = poll;
            TopicId = topicId;
            PageNum = pageNum;
            QueryString = queryString;
            IsCurrentUserMod = isCurrentUserMod;
            IsCurrentUserAdmin = isCurrentUserAdmin;
            IsPreview = isPreview;
            CanVoteNow = !(
                isPreview ||
                (isTopicLocked && !isCurrentUserMod) ||
                currentUser.IsAnonymous ||
                Poll.PollEnded  ||
                (Poll.PollOptions?.Any(o => o.PollOptionVoters?.Any(v => v.UserId == currentUser.UserId) == true) == true && Poll.VoteCanBeChanged != true)
            );
        }
    }
}
