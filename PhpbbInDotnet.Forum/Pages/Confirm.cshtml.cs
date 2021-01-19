using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Email;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Languages;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ConfirmModel : AuthenticatedPageModel
    {
        public string Message { get; private set; }
        
        public string Title { get; private set; }
        
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
        public List<string> Destinations { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SelectedPostIds { get; set; }

        public bool IsModeratorConfirmation { get; private set; } = false;

        public bool IsDestinationConfirmation { get; private set; } = false;

        public ConfirmModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, CommonUtils utils, IConfiguration config, 
            AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils, languageProvider) 
        { }

        public async Task OnGetRegistrationComplete()
        {
            var lang = await GetLanguage();
            Message = LanguageProvider.BasicText[lang, "REGISTRATION_CONFIRM_MESSAGE"];
            Title = LanguageProvider.BasicText[lang, "REGISTRATION_CONFIRM_TITLE"];
        }

        public async Task OnGetConfirmEmail(string code, string username)
        {
            var user = await Context.PhpbbUsers.FirstOrDefaultAsync(u =>
                u.UsernameClean == username &&
                u.UserActkey == code &&
                (u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed || u.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
            );

            var lang = await GetLanguage();

            if (user == null)
            {
                Message = $"<span class=\"fail\">{LanguageProvider.Errors[lang, "REGISTRATION_ERROR"]}</span>";
            }
            else
            {
                Message = $"<span class=\"success\">{LanguageProvider.BasicText[lang, "EMAIL_CONFIRM_MESSAGE"]}</span>";

                user.UserInactiveReason = UserInactiveReason.NewlyRegisteredConfirmed;
                user.UserActkey = string.Empty;
                await Context.SaveChangesAsync();

                var subject = LanguageProvider.Email[LanguageProvider.GetValidatedLanguage(null, HttpContext.Request), "NEWUSER_SUBJECT"];
                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", Config.GetValue<string>("ForumName")),
                    Subject = subject,
                    Body = await Utils.RenderRazorViewToString(
                        "_NewUserNotification",
                        new _NewUserNotificationModel(user.Username),
                        PageContext,
                        HttpContext
                    ),
                    IsBodyHtml = true
                };

                var adminEmails = await (
                    from u in Context.PhpbbUsers.AsNoTracking()
                    join ug in Context.PhpbbUserGroup.AsNoTracking()
                    on u.UserId equals ug.UserId
                    into joined
                    from j in joined
                    where j.GroupId == Constants.ADMIN_GROUP_ID && !string.IsNullOrWhiteSpace(u.UserEmail)
                    select u.UserEmail.Trim()
                ).ToListAsync();

                emailMessage.To.Add(string.Join(',', adminEmails));
                await Utils.SendEmail(emailMessage);
            }
            Title = LanguageProvider.BasicText[lang, "EMAIL_CONFIRM_TITLE"];
        }

        public async Task OnGetNewPassword()
        {
            var lang = await GetLanguage();
            Message = $"<span class=\"success\">{LanguageProvider.BasicText[lang, "NEW_PASSWORD_MESSAGE"]}</span>";
            Title = LanguageProvider.BasicText[lang, "NEW_PASSWORD_TITLE"];
        }

        public async Task OnGetPasswordChanged()
        {
            var lang = await GetLanguage();
            Message = $"<span class=\"success\">{LanguageProvider.BasicText[lang, "NEW_PASSWORD_COMPLETE"]}</span>";
            Title = LanguageProvider.BasicText[lang, "NEW_PASSWORD_TITLE"];
        }

        public async Task OnGetModeratorConfirmation()
        {
            var lang = await GetLanguage();
            IsModeratorConfirmation = true;
            if (ShowTopicSelector)
            {
                Title = LanguageProvider.BasicText[lang, "CHOOSE_DESTINATION_FORUM_TOPIC"];
            }
            else
            {
                Title = LanguageProvider.BasicText[lang, "CHOOSE_DESTINATION_FORUM"];
            }
        }

        public async Task OnGetDestinationConfirmation()
        {
            IsDestinationConfirmation = true;
            Message = $"<span class=\"success\">{LanguageProvider.BasicText[await GetLanguage(), "GENERIC_SUCCESS"]}</span>";
        }
    }
}