using LazyCache;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Models
{
    public abstract class BasePostingModel : AuthenticatedPageModel
    {
        public BasePostingModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cacheService, ILogger logger, ITranslationProvider translationProvider)
            : base(context, forumService, userService, cacheService, logger, translationProvider)
        { }

        [BindProperty]
        public string? PostTitle { get; set; }

        [BindProperty]
        public string? PostText { get; set; }

        protected Task<IActionResult> WithValidInput(Func<Task<IActionResult>> toDo)
            => WithValidInputCore(toDo, PageWithError);

        protected Task<IActionResult> WithValidInput(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
            => WithValidInputCore(toDo, (errorKey, errorMessage) => PageWithError(curForum, errorKey, errorMessage));

        protected virtual IActionResult PageWithError(PhpbbForums phpbbForums, string errorKey, string errorMessage)
            => PageWithError(errorKey, errorMessage);

        private async Task<IActionResult> WithValidInputCore(Func<Task<IActionResult>> success, Func<string, string, IActionResult> fail)
        {
            var lang = Language;
            if ((PostTitle?.Trim()?.Length ?? 0) < 3)
            {
                return fail(nameof(PostTitle), TranslationProvider.Errors[lang, "TITLE_TOO_SHORT"]);
            }

            if ((PostTitle?.Length ?? 0) > 255)
            {
                return fail(nameof(PostTitle), TranslationProvider.Errors[lang, "TITLE_TOO_LONG"]);
            }

            if ((PostText?.Trim()?.Length ?? 0) < 3)
            {
                return fail(nameof(PostText), TranslationProvider.Errors[lang, "POST_TOO_SHORT"]);
            }

            return await success();
        }
    }
}
