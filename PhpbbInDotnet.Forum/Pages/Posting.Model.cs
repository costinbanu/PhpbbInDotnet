using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Caching;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public string? DeleteFileDummyForValidation { get; set; }

        [BindProperty]
        public string? EditReason { get; set; }

        [BindProperty]
        public long? PostTime { get; set; }

        [BindProperty]
        public PhpbbTopics? CurrentTopic { get; set; }

        [BindProperty(SupportsGet = true)]
        public PostingActions? Action { get; set; }

        [BindProperty]
        public long? LastPostTime { get; set; }

        [BindProperty]
        public string? ReturnUrl { get; set; }

		[BindProperty]
		public PhpbbDrafts? ExistingPostDraft { get; set; }

        [BindProperty]
        public List<PhpbbAttachments>? Attachments { get; set; }

        [BindProperty]
        public List<int>? AttachmentOrder { get; set; }

        [BindProperty]
        public bool AttachmentOrderHasChanged { get; set; }

		public PostDto? PreviewablePost { get; private set; }
        public PollDto? PreviewablePoll { get; private set; }
        public bool ShowAttach { get; private set; } = false;
        public bool ShowPoll { get; private set; } = false;
        public PhpbbForums? CurrentForum { get; private set; }
        public bool? SaveDraftSuccess { get; private set; }
        public string? SaveDraftMessage { get; private set; }
        public string? DeleteDraftMessage { get; private set; }
		public bool? DeleteDraftSuccess { get; private set; }
        public string? QuotedPostText { get; private set; }
        public string? QuotedPostAuthor { get; private set; }
        public List<QuotedAttachment>? QuotedAttachments { get; private set; }

        private string CookieBackupKeyPrefix => $"{nameof(PostingBackup)}_{ForumUser.UserId}";
		private string CookieBackupKey => $"{CookieBackupKeyPrefix}_{ForumId}_{(Action == PostingActions.NewTopic ? 0 : (TopicId ?? 0))}_{PostId ?? 0}";
		private IEnumerable<string> PollOptionsEnumerable 
            => (PollOptions?
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))).EmptyIfNull();

        private readonly IPostService _postService;
        private readonly IStorageService _storageService;
        private readonly IWritingToolsService _writingService;
        private readonly IBBCodeRenderingService _renderingService;
        private readonly ILogger _logger;
		private readonly IImageResizeService _imageResizeService;
        private readonly IModeratorService _moderatorService;
		private readonly INotificationService _notificationService;
		private readonly ICachedDbInfoService _cachedDbInfoService;

		static readonly TimeSpan _cookieBackupExpiration = TimeSpan.FromHours(4);

        public PostingModel(IPostService postService, IStorageService storageService, IWritingToolsService writingService, IBBCodeRenderingService renderingService, 
            IConfiguration config, ILogger logger, IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider,
            IImageResizeService imageResizeService, IModeratorService moderatorService, INotificationService notificationService, ICachedDbInfoService cachedDbInfoService)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            _postService = postService;
            _storageService = storageService;
            _writingService = writingService;
            _renderingService = renderingService;
            _logger = logger;
            _imageResizeService = imageResizeService;
            _moderatorService = moderatorService;
            _notificationService = notificationService;
            _cachedDbInfoService = cachedDbInfoService;
        }
    }
}
