using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ConfirmModel : AuthenticatedPageModel
    {
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly IForumDbContext _dbContext;

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

        public ConfirmModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IConfiguration config, IEmailService emailService, IForumDbContext dbContext)
            : base(forumService, userService, sqlExecuter, translationProvider)
        {
            _config = config;
            _emailService = emailService;
            _dbContext = dbContext;
        }

        public void OnGetRegistrationComplete()
        {
            var lang = Language;
            Message = string.Format(TranslationProvider.BasicText[lang, "REGISTRATION_CONFIRM_MESSAGE_FORMAT"], _config.GetValue<string>("AdminEmail"));
            Title = TranslationProvider.BasicText[lang, "REGISTRATION_CONFIRM_TITLE"];
        }

        public Task<IActionResult> OnGetSendConfirmationEmail()
         => WithRegisteredUser(async user =>
         {
             var subject = string.Format(TranslationProvider.BasicText[Language, "VERIFY_EMAIL_ADDRESS_FORMAT"], _config.GetValue<string>("ForumName"));
             var registrationCode = Guid.NewGuid().ToString("n");
             var emailAddress = user.EmailAddress!;
             var dbUser = await _dbContext.PhpbbUsers.FirstAsync(u => u.UserId == user.UserId);
             dbUser.UserActkey = registrationCode;

             await _emailService.SendEmail(
                to: emailAddress,
                subject: subject,
                bodyRazorViewName: "_WelcomeEmailPartial",
                bodyRazorViewModel: new WelcomeEmailDto(subject, registrationCode, dbUser.Username, dbUser.UserLang));

             await _dbContext.SaveChangesAsync();

             Message = $"<span class=\"message success\">{string.Format(TranslationProvider.BasicText[Language, "VERIFICATION_EMAIL_SENT_FORMAT"], emailAddress)}</span>";
             return Page();
         });

        public async Task OnGetConfirmEmail(string code, string username)
        {
            var user = _dbContext.PhpbbUsers.FirstOrDefault(u =>
                u.UsernameClean == username &&
                u.UserActkey == code && (
                    u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed || 
                    u.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed ||
                    u.UserInactiveReason == UserInactiveReason.Active_NotConfirmed));

            var lang = Language;

            if (user == null)
            {
                Message = $"<span class=\"message fail\">{string.Format(TranslationProvider.Errors[lang, "REGISTRATION_ERROR_FORMAT"], _config.GetValue<string>("AdminEmail"))}</span>";
            }
            else
            {
                if (user.UserInactiveReason == UserInactiveReason.Active_NotConfirmed)
                {
                    Message = $"<span class=\"message success\">{TranslationProvider.BasicText[lang, "EMAIL_VERIFICATION_SUCCESSFUL"]}</span>";

                    user.UserInactiveReason = UserInactiveReason.NotInactive;
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

                    var admins = await (
                        from u in _dbContext.PhpbbUsers.AsNoTracking()
                        join ug in _dbContext.PhpbbUserGroup.AsNoTracking()
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
                user.UserActkey = string.Empty;
                await _dbContext.SaveChangesAsync();
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
            var treeTask = ForumService.GetForumTree(ForumUser, false, false);
            var topicDataTask = ShowTopicSelector ? (
                from t in _dbContext.PhpbbTopics.AsNoTracking()
                select new MiniTopicDto
                {
                    ForumId = t.ForumId,
                    TopicId = t.TopicId,
                    TopicTitle = t.TopicTitle,
                    IsLocked = t.TopicStatus == 1
                }).ToListAsync() : Task.FromResult(new List<MiniTopicDto>());
            await Task.WhenAll(treeTask, topicDataTask);

            ForumTree = await treeTask;
            TopicData = await topicDataTask;
        }
    }
}