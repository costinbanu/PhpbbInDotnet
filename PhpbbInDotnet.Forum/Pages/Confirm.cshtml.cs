using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.EmailDtos;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
	public class ConfirmModel : AuthenticatedPageModel
    {
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

        public ConfirmModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IConfiguration config, IEmailService emailService)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            _emailService = emailService;
        }

        public void OnGetRegistrationComplete()
        {
            var lang = Language;
            Message = string.Format(TranslationProvider.BasicText[lang, "REGISTRATION_CONFIRM_MESSAGE_FORMAT"], Configuration.GetValue<string>("AdminEmail"));
            Title = TranslationProvider.BasicText[lang, "REGISTRATION_CONFIRM_TITLE"];
        }

        public Task<IActionResult> OnGetSendConfirmationEmail()
         => WithRegisteredUser(async user =>
         {
             var subject = string.Format(TranslationProvider.BasicText[Language, "VERIFY_EMAIL_ADDRESS_FORMAT"], Configuration.GetValue<string>("ForumName"));
             var registrationCode = Guid.NewGuid().ToString("n");
             var emailAddress = user.EmailAddress!;
             var dbUser = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                 "SELECT * FROM phpbb_users WHERE user_id = @userId",
                 new { user.UserId });

             await _emailService.SendEmail(
                to: emailAddress,
                subject: subject,
                bodyRazorViewName: "_WelcomeEmailPartial",
                bodyRazorViewModel: new WelcomeEmailDto(subject, registrationCode, dbUser.Username, dbUser.UserLang));

             await SqlExecuter.ExecuteAsync(
                 "UPDATE phpbb_users SET user_actkey = @registrationCode",
                 new { registrationCode });

             Message = $"<span class=\"message success\">{string.Format(TranslationProvider.BasicText[Language, "VERIFICATION_EMAIL_SENT_FORMAT"], emailAddress)}</span>";
             return Page();
         });

        public async Task OnGetConfirmEmail(string code, string username)
        {
            var user = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                @"SELECT * 
                    FROM phpbb_users
                   WHERE username_clean = @username
                     AND user_actkey = @code
                     AND (
                            user_inactive_reason = @newlyRegisteredNotConfirmed
                         OR user_inactive_reason = @changedEmailNotConfirmed
                         OR user_inactive_reason = @activeNotConfirmed);",
                new
                {
                    username,
                    code,
                    newlyRegisteredNotConfirmed = UserInactiveReason.NewlyRegisteredNotConfirmed,
                    changedEmailNotConfirmed = UserInactiveReason.ChangedEmailNotConfirmed,
                    activeNotConfirmed = UserInactiveReason.Active_NotConfirmed
                });

            if (user == null)
            {
                Message = $"<span class=\"message fail\">{string.Format(TranslationProvider.Errors[Language, "REGISTRATION_ERROR_FORMAT"], Configuration.GetValue<string>("AdminEmail"))}</span>";
            }
            else
            {
				var newInactiveReason = user.UserInactiveReason;
                var newInactiveTime = user.UserInactiveTime;
                var shouldNotifyAdmins = false;
				if (user.UserInactiveReason == UserInactiveReason.Active_NotConfirmed)
                {
                    Message = $"<span class=\"message success\">{TranslationProvider.BasicText[Language, "EMAIL_VERIFICATION_SUCCESSFUL"]}</span>";

                    newInactiveReason = UserInactiveReason.NotInactive;
                    newInactiveTime = 0;
                }
                else
                {
                    Message = string.Format(TranslationProvider.BasicText[Language, "EMAIL_CONFIRM_MESSAGE_FORMAT"], Configuration.GetValue<string>("AdminEmail"));

                    if (user.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed)
                    {
                        newInactiveReason = UserInactiveReason.NewlyRegisteredConfirmed;
                    }
                    else if (user.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
                    {
                        newInactiveReason = UserInactiveReason.ChangedEmailConfirmed;
                    }

                    shouldNotifyAdmins = true;
                }

                await SqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_users 
                         SET user_inactive_reason = @newInactiveReason,
                             user_inactive_time = @newInactiveTime,
                             user_actkey = ''
                       WHERE user_id = @userId",
                    new
                    {
                        newInactiveReason,
                        newInactiveTime,
                        user.UserId
                    });

                if (shouldNotifyAdmins)
                {
                    var admins = await SqlExecuter.QueryAsync<(string UserLang, string UserEmail)>(
                        "SELECT user_lang, user_email FROM phpbb_users WHERE group_id = @ADMIN_GROUP_ID",
                        new { Constants.ADMIN_GROUP_ID });

                    await Task.WhenAll(admins.Where(a => a.UserEmail == "admin@metrouusor.com").Select(admin => 
                        _emailService.SendEmail(
                            to: admin.UserEmail,
                            subject: TranslationProvider.Email[admin.UserLang, "NEWUSER_SUBJECT"],
                            bodyRazorViewName: "_NewUserNotification",
                            bodyRazorViewModel: new SimpleEmailBody(user.Username, admin.UserLang))));
                }
            }
            Title = TranslationProvider.BasicText[Language, "EMAIL_CONFIRM_TITLE"];
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
            ForumTree = await ForumService.GetForumTree(ForumUser, false, false);
            if (ShowTopicSelector)
            {
                TopicData = (await SqlExecuter.QueryAsync<MiniTopicDto>(
                    "SELECT forum_id, topic_id, topic_title, CASE WHEN topic_status = 1 THEN 1 ELSE 0 END AS is_locked FROM phpbb_topics")).AsList();
            }
            else
            {
                TopicData = new();
            }
        }
    }
}