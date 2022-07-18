using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using PhpbbInDotnet.Domain.Extensions;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public partial class PostingModel
    {
        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationTopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool QuotePostInDifferentTopic { get; set; }

        [BindProperty]
        public string? PollQuestion { get; set; }

        [BindProperty]
        public string? PollOptions { get; set; }

        [BindProperty]
        public string? PollExpirationDaysString { get; set; }

        [BindProperty]
        public int? PollMaxOptions { get; set; }

        [BindProperty]
        public bool PollCanChangeVote { get; set; }

        [BindProperty]
        public IEnumerable<IFormFile>? Files { get; set; }

        [BindProperty]
        public bool ShouldResize { get; set; } = true;

        [BindProperty]
        public bool ShouldHideLicensePlates { get; set; } = true;

        [BindProperty]
        public List<string> DeleteFileDummyForValidation { get; set; }

        [BindProperty]
        public string? EditReason { get; set; }

        [BindProperty]
        public List<PhpbbAttachments>? Attachments { get; set; }

        [BindProperty]
        public long? PostTime { get; set; }

        [BindProperty]
        public PhpbbTopics? CurrentTopic { get; set; }

        [BindProperty]
        public PostingActions? Action { get; set; }

        [BindProperty]
        public long? LastPostTime { get; set; }

        [BindProperty]
        public string? ReturnUrl { get; set; }

        public PostDto? PreviewablePost { get; private set; }
        public PollDto? PreviewablePoll { get; private set; }
        public bool ShowAttach { get; private set; } = false;
        public bool ShowPoll { get; private set; } = false;
        public PhpbbForums? CurrentForum { get; private set; }
        public bool DraftSavedSuccessfully { get; private set; } = false;
        public Guid? PreviewCorrelationId { get; private set; }
        private IEnumerable<string> PollOptionsEnumerable 
            => (PollOptions?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))).EmptyIfNull();
    }
}
