using CryptSharp.Core;
using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Objects.EmailDtos;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Caching;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
	[IgnoreAntiforgeryToken(Order = 1001), ResponseCache(NoStore = true, Duration = 0)]
    public class UserModel : AuthenticatedPageModel
    {
        [BindProperty]
        public PhpbbUsers? CurrentUser { get; set; }
        
        [BindProperty]
        public string? FirstPassword { get; set; }
        
        [BindProperty]
        public string? SecondPassword { get; set; }
        
        [BindProperty]
        public IFormFile? Avatar { get; set; }
        
        [BindProperty]
        public bool DeleteAvatar { get; set; } = false;
        
        [BindProperty]
        public bool ShowEmail { get; set; } = false;
        
        [BindProperty]
        public string? Email { get; set; }
        
        [BindProperty]
        public string? Birthday { get; set; }
        
        [BindProperty]
        public int? AclRole { get; set; }
        
        [BindProperty]
        public int? GroupId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? UserId { get; set; }

        [BindProperty]
        public int UserRank { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ViewAsAnother { get; set; }

        [BindProperty]
        public bool AllowPM { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ManageFoes { get; set; }

        [BindProperty]
        public int SelectedFoeId { get; set; }

        [BindProperty]
        public int[]? SelectedFoes { get; set; }

        [BindProperty]
        public bool JumpToUnread { get; set; }

        public int TotalPosts { get; private set; }
        public (int? Id, string? Title) PreferredTopic { get; private set; }
        public double PostsPerDay { get; private set; }
        public List<PhpbbUsers>? Foes { get; private set; }
        public long AttachCount { get; private set; }
        public long AttachTotalSize { get; private set; }
        public UserPageMode Mode { get; private set; }
        public bool EmailChanged { get; private set; }

        private readonly IStorageService _storageService;
        private readonly IWritingToolsService _writingService;
        private readonly IOperationLogService _operationLogService;
        private readonly IEmailService _emailService;
        private readonly ILogger _logger;
        private readonly IAppCache _cache;
        private readonly IUserProfileDataValidationService _validationService;
		private readonly IImageResizeService _imageResizeService;
		private readonly ICachedDbInfoService _cachedDbInfoService;
		private const int DB_CACHE_EXPIRATION_MINUTES = 20;

        public UserModel(IStorageService storageService, IWritingToolsService writingService, IOperationLogService operationLogService, IConfiguration config, 
            IEmailService emailService, IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, IImageResizeService imageResizeService,
            ITranslationProvider translationProvider, ILogger logger, IAppCache cache, IUserProfileDataValidationService validationService, ICachedDbInfoService cachedDbInfoService)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            _storageService = storageService;
            _writingService = writingService;
            _operationLogService = operationLogService;
            _emailService = emailService;
            _logger = logger;
            _cache = cache;
            _validationService = validationService;
            _imageResizeService = imageResizeService;
            _cachedDbInfoService = cachedDbInfoService;
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (viewingUser) =>
            {
                if ((UserId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID)
                {
                    return NotFound();
                }

                var cur = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @userId", 
                    new { UserId });
                if (cur == null)
                {
                    return NotFound();
                }
                await Render(cur);

                ManageFoes = (ManageFoes ?? false) && await CanEdit();
                ViewAsAnother = (ViewAsAnother ?? true) && !ManageFoes.Value;
                JumpToUnread = cur.JumpToUnread ?? true;
                
                return Page();
            });

        public async Task<IActionResult> OnPost()
        {
            ThrowIfEntireForumIsReadOnly();

            if (!await CanEdit())
            {
                return Unauthorized();
            }

            var dbUser = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                "SELECT * FROM phpbb_users WHERE user_id = @userId",
                new { CurrentUser?.UserId });
                
            if (dbUser == null)
            {
                return NotFound();
            }

            var currentUserId = ForumUser.UserId;
            var isSelf = CurrentUser!.UserId == currentUserId;
            var userShouldSignIn = dbUser.UserAllowPm.ToBool() != AllowPM || dbUser.UserDateformat != CurrentUser.UserDateformat;
            var lang = Language;

            var newCleanUsername = StringUtility.CleanString(CurrentUser.Username);
            var usernameChanged = false;
            var oldUsername = dbUser.Username;
            var shouldInvalidateCache = false;

            if (await UserService.IsAdmin(ForumUser) && dbUser.UsernameClean != newCleanUsername && !string.IsNullOrWhiteSpace(CurrentUser.Username))
            {
                if (!_validationService.ValidateUsername(nameof(CurrentUser), CurrentUser.Username))
                {
                    return Page();
                }

                var usersWithSameUsername = await SqlExecuter.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_users WHERE username_clean = @newCleanUsername",
                    new { newCleanUsername});
                if (usersWithSameUsername > 0)
                {
                    return PageWithError(nameof(CurrentUser), TranslationProvider.Errors[lang, "EXISTING_USERNAME"]);
                }

                dbUser.Username = CurrentUser.Username;
                dbUser.UsernameClean = newCleanUsername;

                await SqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_forums SET forum_last_poster_name = @username WHERE forum_last_poster_id = @userId;
                      UPDATE phpbb_topics SET topic_last_poster_name = @username WHERE topic_last_poster_id = @userId",
                    new
                    {
                        dbUser.UserId,
                        dbUser.Username
                    });

                userShouldSignIn = true;
                usernameChanged = true;
                shouldInvalidateCache = true;
            }

            if (!string.IsNullOrWhiteSpace(Birthday) && Birthday != dbUser.UserBirthday)
            {
                if (!DateTime.TryParseExact(Birthday, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var birthday) ||
                    DateTime.UtcNow.Subtract(birthday).TotalDays / 365.25 < (Configuration.GetValue<int?>("MinimumAge") ?? 16))
                {
                    return PageWithError(nameof(Birthday), TranslationProvider.Errors[lang, "INVALID_DATE"]);
                }
            }

            dbUser.UserBirthday = Birthday ?? string.Empty;
            dbUser.UserAllowViewemail = ShowEmail.ToByte();
            dbUser.UserAllowPm = AllowPM.ToByte();
            dbUser.UserOcc = CurrentUser.UserOcc ?? string.Empty;
            dbUser.UserFrom = CurrentUser.UserFrom ?? string.Empty;
            dbUser.UserInterests = CurrentUser.UserInterests ?? string.Empty;
            dbUser.UserDateformat = CurrentUser.UserDateformat ?? TranslationProvider.GetDefaultDateFormat(lang);
            dbUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : await _writingService.PrepareTextForSaving(CurrentUser.UserSig);
            dbUser.UserEditTime = CurrentUser.UserEditTime;
            dbUser.UserWebsite = CurrentUser.UserWebsite ?? string.Empty;
            if (UserRank > 0)
            {
                dbUser.UserRank = UserRank;
            }
            dbUser.UserStyle = CurrentUser.UserStyle;
            dbUser.JumpToUnread = JumpToUnread;
            dbUser.UserLang = CurrentUser.UserLang;

            var newColour = CurrentUser.UserColour?.TrimStart('#');
            if (!string.IsNullOrWhiteSpace(newColour) && dbUser.UserColour != newColour)
            {
                dbUser.UserColour = newColour;
                await SqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_forums SET forum_last_poster_colour = @newColour WHERE forum_last_poster_id = @userId;
                      UPDATE phpbb_topics SET topic_last_poster_colour = @newColour WHERE topic_last_poster_id = @userId",
                    new
                    {
                        dbUser.UserId,
                        newColour
                    });
                shouldInvalidateCache = true;
            }

            var newEmailHash = HashUtility.ComputeCrc64Hash(Email!);
            var oldEmailAddress = string.Empty;
            if (newEmailHash != dbUser.UserEmailHash)
            {
                if (!_validationService.ValidateEmail(nameof(Email), Email))
                {
                    return Page();
                }

                var usersWithSameEmail = await SqlExecuter.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_users WHERE user_email_hash = @newEmailHash",
                    new { newEmailHash });
                if (usersWithSameEmail > 0)
                {
                    return PageWithError(nameof(Email), TranslationProvider.Errors[lang, "EXISTING_EMAIL"]);
                }

                oldEmailAddress = dbUser.UserEmail;

                dbUser.UserEmail = Email!;
                dbUser.UserEmailHash = newEmailHash;
                dbUser.UserEmailtime = DateTime.UtcNow.ToUnixTimestamp();

                if (isSelf)
                {
                    var registrationCode = Guid.NewGuid().ToString("n");

                    dbUser.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                    dbUser.UserInactiveReason = UserInactiveReason.ChangedEmailNotConfirmed;
                    dbUser.UserActkey = registrationCode;

                    var subject = string.Format(TranslationProvider.Email[dbUser.UserLang, "EMAIL_CHANGED_SUBJECT_FORMAT"], Configuration.GetValue<string>("ForumName"));
                    await _emailService.SendEmail(
                        to: Email!,
                        subject: subject,
                        bodyRazorViewName: "_WelcomeEmailPartial",
                        bodyRazorViewModel: new WelcomeEmailDto(subject, registrationCode, dbUser.Username, dbUser.UserLang));

                    userShouldSignIn = true;
                }

                EmailChanged = true;
            }

            var passwordChanged = false;
            if (!string.IsNullOrWhiteSpace(FirstPassword) && Crypter.Phpass.Crypt(FirstPassword, dbUser.UserPassword) != dbUser.UserPassword)
            {
                var validations = new[]
                {
                    _validationService.ValidatePassword(nameof(FirstPassword), FirstPassword),
                    _validationService.ValidateSecondPassword(nameof(SecondPassword), SecondPassword, FirstPassword)
                };

                if (!validations.All(x => x))
                {
                    return Page();
                }

                dbUser.UserPassword = Crypter.Phpass.Crypt(FirstPassword, Crypter.Phpass.GenerateSalt());
                dbUser.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
                userShouldSignIn = true;
                passwordChanged = true;
            }

            if (DeleteAvatar && !string.IsNullOrWhiteSpace(dbUser.UserAvatar))
            {
                if (!await _storageService.DeleteAvatar(dbUser.UserId, dbUser.UserAvatar))
                {
                    return PageWithError(nameof(Avatar), TranslationProvider.Errors[lang, "DELETE_AVATAR_ERROR"]);
                }

                dbUser.UserAvatarType = 0;
                dbUser.UserAvatarWidth = 0;
                dbUser.UserAvatarHeight = 0;
                dbUser.UserAvatar = string.Empty;
            }

            if (Avatar != null)
            {
                Stream? output = null;
                try
                {
                    var maxSize = Configuration.GetObject<ImageSize>("AvatarMaxSize");
					using var input = Avatar.OpenReadStream();
                    using var image = await Image.LoadAsync(input);
                    output = await _imageResizeService.ResizeImageByResolution(image, Avatar.FileName, Math.Max(maxSize.Width, maxSize.Height));
                    if (!string.IsNullOrWhiteSpace(dbUser.UserAvatar))
                    {
                        await _storageService.DeleteAvatar(dbUser.UserId, dbUser.UserAvatar);
                    }
                    if (!await _storageService.UploadAvatar(dbUser.UserId, output ?? input, Avatar.FileName))
                    {
                        return PageWithError(nameof(Avatar), TranslationProvider.Errors[lang, "AVATAR_UPLOAD_ERROR"]);
                    }
                    else
                    {
                        dbUser.UserAvatarType = 1;
                        dbUser.UserAvatarWidth = unchecked((short)image.Width);
                        dbUser.UserAvatarHeight = unchecked((short)image.Height);
                        dbUser.UserAvatar = Avatar.FileName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to upload avatar for {user}", CurrentUser?.Username ?? dbUser?.Username ?? "N/A");
                    return PageWithError(nameof(Avatar), TranslationProvider.Errors[lang, "AVATAR_UPLOAD_ERROR"]);
                }
                finally
                {
                    output?.Dispose();
                }
            }

            var userRoles = (await UserService.GetUserRolesLazy()).Select(r => r.RoleId);
            var dbAclRole = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbAclUsers>(
                "SELECT * FROM phpbb_acl_users WHERE user_id = @userId AND auth_role_id IN @userRoles",
                new
                {
                    dbUser.UserId,
                    userRoles = userRoles.DefaultIfNullOrEmpty()
                });
                
            if (dbAclRole != null && dbAclRole.AuthRoleId != (AclRole ?? -1))
            {
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_acl_users WHERE user_id = @userId AND forum_id = @forumId",
                    dbAclRole);
                if ((AclRole ?? -1) != -1)
                {
                    await SqlExecuter.ExecuteAsync(
                        "INSERT INTO phpbb_acl_users(auth_option_id, auth_role_id, auth_setting, forum_id, user_id) VALUES(0, @aclRole, 0, 0, @userId)",
                        new
                        {
                            AclRole,
                            dbUser.UserId,
                        });
                }
                userShouldSignIn = true;
            }

            var dbUserGroup = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUserGroup>(
                "SELECT * FROM phpbb_user_group WHERE user_id = @userId",
                new { dbUser.UserId });
            if (dbUserGroup == null)
            {
                if (dbUser.GroupId == 0)
                {
                    throw new InvalidOperationException($"User {dbUser.UserId} has no group associated neither in phpbb_users, nor in phpbb_user_group.");
                }
                await SqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_user_group(group_id, user_id) VALUES(@groupId, @userId",
                    new
                    {
                        dbUser.GroupId,
                        dbUser.UserId
                    });
            }
            else if (GroupId.HasValue && GroupId != dbUserGroup.GroupId)
            {
                await SqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_user_group SET group_id = @newGroupId WHERE group_id = @oldGroupId AND user_id = @userId",
                    new
                    {
                        newGroupId = GroupId.Value,
                        oldGroupId = dbUserGroup.GroupId,
                        dbUser.UserId
                    });

                var group = await SqlExecuter.QuerySingleAsync<PhpbbGroups>(
                    "SELECT * FROM phpbb_groups WHERE group_id = @groupId",
                    new { GroupId });
                await SqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_forums SET forum_last_poster_colour = @newColour WHERE forum_last_poster_id = @userId;
                      UPDATE phpbb_topics SET topic_last_poster_colour = @newColour WHERE topic_last_poster_id = @userId",
                    new
                    {
                        dbUser.UserId,
                        newColour = group.GroupColour
                    });

                dbUser.UserColour = group!.GroupColour;
                dbUser.GroupId = group.GroupId;
                userShouldSignIn = true;
                shouldInvalidateCache = true;
            }


            dbUser.UserShouldSignIn = userShouldSignIn;

            var affectedEntries = 0;
            try
            {
                affectedEntries = await SqlExecuter.ExecuteAsync(
                    @"UPDATE dbo.phpbb_users
                       SET user_type  = @UserType,
                           group_id  = @GroupId,
                           user_permissions  = @UserPermissions,
                           user_perm_from  = @UserPermFrom,
                           user_ip  = @UserIp,
                           user_regdate  = @UserRegdate,
                           username  = @Username,
                           username_clean  = @UsernameClean,
                           user_password  = @UserPassword,
                           user_passchg  = @UserPasschg,
                           user_pass_convert  = @UserPassConvert,
                           user_email  = @UserEmail,
                           user_email_hash  = @UserEmailHash,
                           user_birthday  = @UserBirthday,
                           user_lastvisit  = @UserLastvisit,
                           user_lastmark  = @UserLastmark,
                           user_lastpost_time  = @UserLastpostTime,
                           user_lastpage  = @UserLastpage,
                           user_last_confirm_key  = @UserLastConfirmKey,
                           user_last_search  = @UserLastSearch,
                           user_warnings  = @UserWarnings,
                           user_last_warning  = @UserLastWarning,
                           user_login_attempts  = @UserLoginAttempts,
                           user_inactive_reason  = @UserInactiveReason,
                           user_inactive_time  = @UserInactiveTime,
                           user_posts  = @UserPosts,
                           user_lang  = @UserLang,
                           user_timezone  = @UserTimezone,
                           user_dst  = @UserDst,
                           user_dateformat  = @UserDateformat,
                           user_style  = @UserStyle,
                           user_rank  = @UserRank,
                           user_colour  = @UserColour,
                           user_new_privmsg  = @UserNewPrivmsg,
                           user_unread_privmsg  = @UserUnreadPrivmsg,
                           user_last_privmsg  = @UserLastPrivmsg,
                           user_message_rules  = @UserMessageRules,
                           user_full_folder  = @UserFullFolder,
                           user_emailtime  = @UserEmailtime,
                           user_topic_show_days  = @UserTopicShowDays,
                           user_topic_sortby_type  = @UserTopicSortbyType,
                           user_topic_sortby_dir  = @UserTopicSortbyDir,
                           user_post_show_days  = @UserPostShowDays,
                           user_post_sortby_type  = @UserPostSortbyType,
                           user_post_sortby_dir  = @UserPostSortbyDir,
                           user_notify  = @UserNotify,
                           user_notify_pm  = @UserNotifyPm,
                           user_notify_type  = @UserNotifyType,
                           user_allow_pm  = @UserAllowPm,
                           user_allow_viewonline  = @UserAllowViewonline,
                           user_allow_viewemail  = @UserAllowViewemail,
                           user_allow_massemail  = @UserAllowMassemail,
                           user_options  = @UserOptions,
                           user_avatar  = @UserAvatar,
                           user_avatar_type  = @UserAvatarType,
                           user_avatar_width  = @UserAvatarWidth,
                           user_avatar_height  = @UserAvatarHeight,
                           user_sig  = @UserSig,
                           user_sig_bbcode_uid  = @UserSigBbcodeUid,
                           user_sig_bbcode_bitfield  = @UserSigBbcodeBitfield,
                           user_from  = @UserFrom,
                           user_icq  = @UserIcq,
                           user_aim  = @UserAim,
                           user_yim  = @UserYim,
                           user_msnm  = @UserMsnm,
                           user_jabber  = @UserJabber,
                           user_website  = @UserWebsite,
                           user_occ  = @UserOcc,
                           user_interests  = @UserInterests,
                           user_actkey  = @UserActkey,
                           user_newpasswd  = @UserNewpasswd,
                           user_form_salt  = @UserFormSalt,
                           user_new  = @UserNew,
                           user_reminded  = @UserReminded,
                           user_reminded_time  = @UserRemindedTime,
                           user_edit_time  = @UserEditTime,
                           jump_to_unread  = @JumpToUnread,
                           user_should_sign_in  = @UserShouldSignIn
                     WHERE user_id = @UserId",
                    dbUser);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating user profile for {user}", CurrentUser?.Username ?? dbUser?.Username ?? "N/A");
                return PageWithError(nameof(CurrentUser), TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
            }

            if (EmailChanged)
            {
                await _operationLogService.LogUserProfileAction(UserProfileActions.ChangeEmail, currentUserId, dbUser, $"Old e-mail address: '{oldEmailAddress}'.");
            }
            if (passwordChanged)
            {
                await _operationLogService.LogUserProfileAction(UserProfileActions.ChangePassword, currentUserId, dbUser);
            }
            if (usernameChanged)
            {
                await _operationLogService.LogUserProfileAction(UserProfileActions.ChangeUsername, currentUserId, dbUser, $"Old username: '{oldUsername}'");
            }
            if (shouldInvalidateCache)
            {
                await _cachedDbInfoService.ForumTree.InvalidateAsync();
            }

            if (affectedEntries > 0 && isSelf)
            {
                Mode = UserPageMode.Edit;
                return await OnGet();
            }

            await Render(dbUser);
            
            return Page();
        }

        public async Task<IActionResult> OnPostAddFoe()
            => await WithRegisteredUser(async (user) =>
            {
                if (!await CanAddFoe())
                {
                    _logger.Error("Potential cross site forgery attempt in {name}", nameof(OnPostAddFoe));
                    return await PageWithErrorAsync(nameof(CurrentUser), TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED"], toDoBeforeReturn: () => Mode = UserPageMode.AddFoe, resultFactory: OnGet);
                }
                var cur = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @userId",
                    new { UserId });
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id = @otherId;" +
                    "INSERT INTO phpbb_zebra (user_id, zebra_id, friend, foe) VALUES (@userId, @otherId, 0, 1)",
                    new { user.UserId, otherId = cur!.UserId }
                );
                Mode = UserPageMode.AddFoe;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRemoveFoe()
            => await WithRegisteredUser(async (user) =>
            {
                if (!CanRemoveFoe())
                {
                    _logger.Error("Potential cross site forgery attempt in {name}", nameof(OnPostRemoveFoe));
                    return await PageWithErrorAsync(nameof(CurrentUser), TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED"], toDoBeforeReturn: () => Mode = UserPageMode.AddFoe, resultFactory: OnGet);
                }
                var cur = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @userId",
                    new { UserId });
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id = @otherId;",
                    new { user.UserId, otherId = cur!.UserId }
                );
                Mode = UserPageMode.RemoveFoe;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRemoveMultipleFoes()
            => await WithRegisteredUser(async (user) =>
            {
                if (!await CanEdit())
                {
                    return Unauthorized();
                }
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id IN @otherIds;",
                    new { user.UserId, otherIds = SelectedFoes!.DefaultIfEmpty() }
                );
                Mode = UserPageMode.RemoveMultipleFoes;
                return await OnGet();
            });

        public async Task<bool> CanEdit() 
            => !(ViewAsAnother ?? false) && (ForumUser.UserId == CurrentUser!.UserId || await UserService.IsAdmin(ForumUser));

        public async Task<bool> CanAddFoe()
        {
            var viewingUser = ForumUser;
            var pageUser = await UserService.ExpandForumUser(UserService.DbUserToForumUser(CurrentUser!), ForumUserExpansionType.Permissions);
            return !await UserService.IsUserModeratorInForum(pageUser, 0) && !await UserService.IsUserModeratorInForum(viewingUser, 0) && !(viewingUser.Foes?.Contains(pageUser.UserId) ?? false);
        }

        public bool CanRemoveFoe()
        {
            var viewingUser = ForumUser;
            var pageUser = UserService.DbUserToForumUser(CurrentUser!);
            return viewingUser.Foes?.Contains(pageUser.UserId) ?? false;
        }

        public async Task<List<PhpbbLang>> GetLanguages()
            => await _cache.GetOrAddAsync(
                key: nameof(PhpbbLang),
                addItemFactory: async () => (await SqlExecuter.QueryAsync<PhpbbLang>("SELECT * FROM phpbb_lang")).AsList(),
                expires: DateTimeOffset.UtcNow.AddMinutes(DB_CACHE_EXPIRATION_MINUTES)
            );

        private async Task Render(PhpbbUsers cur)
        {
            var tree = await ForumService.GetForumTree(ForumUser, false, false);
            CurrentUser = cur;
            CurrentUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : _writingService.CleanBbTextForDisplay(CurrentUser.UserSig, CurrentUser.UserSigBbcodeUid);
            TotalPosts = cur.UserPosts;
            PreferredTopic = await GetPreferredTopic(tree);
            PostsPerDay = TotalPosts / DateTime.UtcNow.Subtract(cur.UserRegdate.ToUtcTime()).TotalDays;
            Email = cur.UserEmail;
            Birthday = cur.UserBirthday;
            var currentAuthenticatedUser = await UserService.ExpandForumUser(UserService.DbUserToForumUser(cur), ForumUserExpansionType.Permissions);
            AclRole = await GetRole();
            var group = await UserService.GetUserGroup(cur.UserId);
            GroupId = group!.GroupId;
            UserRank = cur.UserRank == 0 ? group.GroupRank : cur.UserRank;
            AllowPM = cur.UserAllowPm.ToBool();
            ShowEmail = cur.UserAllowViewemail.ToBool();
            Foes = (await SqlExecuter.QueryAsync<PhpbbUsers>(
                @"SELECT u.*
                    FROM phpbb_users u
                    JOIN phpbb_zebra z ON z.zebra_id = u.user_id
                   WHERE z.user_id = @userId AND z.foe = 1",
                new { cur.UserId })).AsList();
            var result = await SqlExecuter.QueryFirstOrDefaultAsync<(long size, long cnt)>(
                "SELECT sum(a.filesize) as size, count(a.attach_id) as cnt " +
                "FROM phpbb_attachments a " +
                "JOIN phpbb_posts p ON a.post_msg_id = p.post_id " +
                "WHERE p.poster_id = @userId",
                new { cur.UserId });
            AttachCount = result.cnt;
            AttachTotalSize = result.size;

            async Task<(int? id, string? title)> GetPreferredTopic(HashSet<ForumTree> tree)
            {
                var restrictedForums = (await ForumService.GetRestrictedForumList(ForumUser)).Select(f => f.forumId).DefaultIfEmpty();
                var (forumId, topicId, topicTitle) = await SqlExecuter.QueryFirstOrDefaultAsync<(int forumId, int topicId, string topicTitle)>(
                    @"SELECT t.forum_id, t.topic_id, t.topic_title, count(p.post_id)
                        FROM phpbb_posts p
                        JOIN phpbb_topics t on p.topic_id = t.topic_id
                       WHERE p.poster_id = @userId AND t.forum_id NOT IN @restrictedForums
                       GROUP BY t.forum_id, t.topic_id, t.topic_title
                       ORDER BY count(p.post_id) DESC",
                    new
                    {
                        cur.UserId,
                        restrictedForums
                    });
                    
                string? preferredTopicTitle = null;
                if (topicId > 0)
                {
                    preferredTopicTitle = ForumService.GetPathText(tree, forumId);
                    return (topicId, preferredTopicTitle + Constants.FORUM_PATH_SEPARATOR + topicTitle);
                }
                return (null, null);
            }

            async Task<int?> GetRole()
            {
                var currentAuthenticatedUser = await UserService.ExpandForumUser(UserService.DbUserToForumUser(cur), ForumUserExpansionType.Permissions);
                return await UserService.GetUserRole(currentAuthenticatedUser);
            }
        }
    }
}