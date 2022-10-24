using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ConfirmModel : AuthenticatedPageModel
    {
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public string? Message { get; private set; }
        
        public string? Title { get; private set; }
        
        [BindProperty(SupportsGet = true)]
        public int? ForumId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorPostActions? PostAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool ShowTopicSelector { get; set; }

        [BindProperty(SupportsGet = true)]
        public List<string>? Destinations { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedPostIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedTopicIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Destination { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? QuotePostInDifferentTopic { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DestinationHandler { get; set; }

        public bool IsDestinationPicker { get; private set; } = false;

        public bool IsModeratorConfirmation { get; private set; } = false;

        public bool IsDestinationConfirmation { get; private set; } = false;
        public HashSet<ForumTree>? ForumTree { get; private set; }
        public List<MiniTopicDto>? TopicData { get; private set; }

        public ConfirmModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache, ILogger logger, 
            IConfiguration config, ITranslationProvider translationProvider, IEmailService emailService)
            : base(context, forumService, userService, cache, logger, translationProvider) 
        {
            _config = config;
            _emailService = emailService;
        }

        public void OnGetRegistrationComplete()
        {
            var lang = Language;
            Message = string.Format(TranslationProvider.BasicText[lang, "REGISTRATION_CONFIRM_MESSAGE_FORMAT"], _config.GetValue<string>("AdminEmail"));
            Title = TranslationProvider.BasicText[lang, "REGISTRATION_CONFIRM_TITLE"];
        }

        public async Task OnGetConfirmEmail(string code, string username)
        {
            var user = Context.PhpbbUsers.FirstOrDefault(u =>
                u.UsernameClean == username &&
                u.UserActkey == code &&
                (u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed || u.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
            );

            var lang = Language;

            if (user == null)
            {
                Message = $"<span class=\"message fail\">{string.Format(TranslationProvider.Errors[lang, "REGISTRATION_ERROR_FORMAT"], _config.GetValue<string>("AdminEmail"))}</span>";
            }
            else
            {
                Message = string.Format(TranslationProvider.BasicText[lang, "EMAIL_CONFIRM_MESSAGE_FORMAT"], _config.GetValue<string>("AdminEmail"));

                if (user.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed)
                {
                    user.UserInactiveReason = UserInactiveReason.NewlyRegisteredConfirmed;
                }
                else if (user.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
                {
                    user.UserInactiveReason = UserInactiveReason.ChangedEmailConfirmed;
                }
                user.UserActkey = string.Empty;
                await Context.SaveChangesAsync();

                var admins = await (
                    from u in Context.PhpbbUsers.AsNoTracking()
                    join ug in Context.PhpbbUserGroup.AsNoTracking()
                    on u.UserId equals ug.UserId
                    into joined
                    from j in joined
                    where j.GroupId == Constants.ADMIN_GROUP_ID
                    select u
                ).ToListAsync();

                await Task.WhenAll(admins.Select(admin =>
                {
                    var subject = TranslationProvider.Email[admin.UserLang, "NEWUSER_SUBJECT"];
                    return _emailService.SendEmail(
                        to: admin.UserEmail,
                        subject: subject,
                        bodyRazorViewName: "_NewUserNotification",
                        bodyRazorViewModel: new SimpleEmailBody(user.Username, admin.UserLang));
                }));
            }
            Title = TranslationProvider.BasicText[lang, "EMAIL_CONFIRM_TITLE"];
        }

        public void OnGetNewPassword()
        {
            var lang = Language;
            Message = $"<span class=\"message success\">{TranslationProvider.BasicText[lang, "NEW_PASSWORD_MESSAGE"]}</span>";
            Title = TranslationProvider.BasicText[lang, "NEW_PASSWORD_TITLE"];
        }

        public void OnGetPasswordChanged()
        {
            var lang = Language;
            Message = $"<span class=\"message success\">{TranslationProvider.BasicText[lang, "NEW_PASSWORD_COMPLETE"]}</span>";
            Title = TranslationProvider.BasicText[lang, "NEW_PASSWORD_TITLE"];
        }

        public Task<IActionResult> OnGetModeratorConfirmation()
            => WithModerator(0, async () =>
            {
                var lang = Language;
                IsModeratorConfirmation = true;
                if (ShowTopicSelector)
                {
                    Title = TranslationProvider.BasicText[lang, "CHOOSE_DESTINATION_FORUM_TOPIC"];
                }
                else
                {
                    Title = TranslationProvider.BasicText[lang, "CHOOSE_DESTINATION_FORUM"];
                }
                await SetFrontendData();

                return Page();
            });

        public void OnGetDestinationConfirmation()
        {
            IsDestinationConfirmation = true;
            Message = $"<span class=\"message success\">{TranslationProvider.BasicText[Language, "GENERIC_SUCCESS"]}</span>";
        }

        public async Task OnGetDestinationPicker()
        {
            IsDestinationPicker = true;
            Message = $"<span class=\"message success\">{TranslationProvider.BasicText[Language, "GENERIC_SUCCESS"]}</span>";
            await SetFrontendData();
        }

        private async Task SetFrontendData()
        {
            var treeTask = GetForumTree(false, false);
            var topicDataTask = ShowTopicSelector ? (
                from t in Context.PhpbbTopics.AsNoTracking()
                select new MiniTopicDto
                {
                    ForumId = t.ForumId,
                    TopicId = t.TopicId,
                    TopicTitle = t.TopicTitle,
                    IsLocked = t.TopicStatus == 1
                }).ToListAsync() : Task.FromResult(new List<MiniTopicDto>());
            await Task.WhenAll(treeTask, topicDataTask);

            ForumTree = (await treeTask).Tree;
            TopicData = await topicDataTask;
        }
    }
}