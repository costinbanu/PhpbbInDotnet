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
using LazyCache;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ConfirmModel : AuthenticatedPageModel
    {
        private readonly IConfiguration _config;

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

        [BindProperty(SupportsGet = true)]
        public string SelectedTopicIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Destination { get; set; }

        public bool IsModeratorConfirmation { get; private set; } = false;

        public bool IsDestinationConfirmation { get; private set; } = false;

        public ConfirmModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils, IConfiguration config, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider) 
        {
            _config = config;
        }

        public void OnGetRegistrationComplete()
        {
            var lang = GetLanguage();
            Message = string.Format(LanguageProvider.BasicText[lang, "REGISTRATION_CONFIRM_MESSAGE_FORMAT"], _config.GetValue<string>("AdminEmail"));
            Title = LanguageProvider.BasicText[lang, "REGISTRATION_CONFIRM_TITLE"];
        }

        public async Task OnGetConfirmEmail(string code, string username)
        {
            var user = await Context.PhpbbUsers.FirstOrDefaultAsync(u =>
                u.UsernameClean == username &&
                u.UserActkey == code &&
                (u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed || u.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
            );

            var lang = GetLanguage();

            if (user == null)
            {
                Message = $"<span class=\"message fail\">{string.Format(LanguageProvider.Errors[lang, "REGISTRATION_ERROR_FORMAT"], _config.GetValue<string>("AdminEmail"))}</span>";
            }
            else
            {
                Message = $"<span class=\"message success\">{string.Format(LanguageProvider.BasicText[lang, "EMAIL_CONFIRM_MESSAGE_FORMAT"], _config.GetValue<string>("AdminEmail"))}</span>";

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

                foreach (var admin in admins)
                {
                    var subject = LanguageProvider.Email[admin.UserLang, "NEWUSER_SUBJECT"];
                    using var emailMessage = new MailMessage
                    {
                        From = new MailAddress(_config.GetValue<string>("AdminEmail"), _config.GetValue<string>("ForumName")),
                        Subject = subject,
                        Body = await Utils.RenderRazorViewToString(
                            "_NewUserNotification",
                            new _NewUserNotificationModel
                            {
                                Username = user.Username,
                                Language = admin.UserLang
                            },
                            PageContext,
                            HttpContext
                        ),
                        IsBodyHtml = true
                    };
                    emailMessage.To.Add(admin.UserEmail.Trim());
                    await Utils.SendEmail(emailMessage);
                }
            }
            Title = LanguageProvider.BasicText[lang, "EMAIL_CONFIRM_TITLE"];
        }

        public void OnGetNewPassword()
        {
            var lang = GetLanguage();
            Message = $"<span class=\"success\">{LanguageProvider.BasicText[lang, "NEW_PASSWORD_MESSAGE"]}</span>";
            Title = LanguageProvider.BasicText[lang, "NEW_PASSWORD_TITLE"];
        }

        public void OnGetPasswordChanged()
        {
            var lang = GetLanguage();
            Message = $"<span class=\"success\">{LanguageProvider.BasicText[lang, "NEW_PASSWORD_COMPLETE"]}</span>";
            Title = LanguageProvider.BasicText[lang, "NEW_PASSWORD_TITLE"];
        }

        public void OnGetModeratorConfirmation()
        {
            var lang = GetLanguage();
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

        public void OnGetDestinationConfirmation()
        {
            IsDestinationConfirmation = true;
            Message = $"<span class=\"success\">{LanguageProvider.BasicText[GetLanguage(), "GENERIC_SUCCESS"]}</span>";
        }
    }
}