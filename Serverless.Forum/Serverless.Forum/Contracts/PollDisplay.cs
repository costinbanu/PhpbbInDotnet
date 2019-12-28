using System;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Contracts
{
    public class PollDisplay
    {
        public int TopicId { get; set; }

        public string PollTitle { get; set; }

        public DateTime PollStart { get; set; }

        public DateTime PollEnd => PollStart.AddSeconds(PollDurationSecons);

        public int PollDurationSecons { get; set; } //86400 seconds in a day

        public List<PollOption> PollOptions { get; set; }

        public int PollMaxOptions { get; set; } = 1;

        public bool VoteCanBeChanged { get; set; } = false;

        public bool CanVoteNow(int? currentUserId) => !(
            (currentUserId ?? 1) <= 1 || 
            PollEnd < DateTime.UtcNow || 
            (PollOptions.Any(o => o.PollOptionVoters.Any(v => v.UserId == currentUserId.Value)) && !VoteCanBeChanged)
        );

        public int TotalVotes => PollOptions.Sum(o => o.PollOptionVotes);
    }

    public class PollOption
    {
        public int PollOptionId { get; set; }

        public int TopicId { get; set; }

        public string PollOptionText { get; set; }

        public List<PollOptionVoter> PollOptionVoters { get; set; }

        public int PollOptionVotes => PollOptionVoters.Count;
    }

    public class PollOptionVoter
    {
        public int? UserId { get; set; }

        public string Username { get; set; }
    }
}
