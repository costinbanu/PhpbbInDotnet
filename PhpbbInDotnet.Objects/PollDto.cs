using System;
using System.Collections.Generic;
using System.Linq;

namespace PhpbbInDotnet.Objects
{
    public class PollDto
    {
        public int TopicId { get; set; }

        public string? PollTitle { get; set; }

        public DateTime PollStart { get; set; }

        public DateTime? PollEnd => PollDurationSecons == 0 ? null : PollStart.AddSeconds(PollDurationSecons);

        public int PollDurationSecons { get; set; } //86400 seconds in a day

        public List<PollOption>? PollOptions { get; set; }

        public int PollMaxOptions { get; set; } = 1;

        public bool VoteCanBeChanged { get; set; } = false;

        public int TotalVotes => PollOptions?.Sum(o => o.PollOptionVotes) ?? 0;

        public bool PollEnded => PollEnd < DateTime.UtcNow;
    }

    public class PollOption
    {
        public byte PollOptionId { get; set; }

        public int TopicId { get; set; }

        public string? PollOptionText { get; set; }

        public List<PollOptionVoter>? PollOptionVoters { get; set; }

        public int PollOptionVotes => PollOptionVoters?.Count ?? 0;
    }

    public class PollOptionVoter
    {
        public int UserId { get; set; }

        public string? Username { get; set; }

        public int PollOptionId { get; set; }
    }
}
