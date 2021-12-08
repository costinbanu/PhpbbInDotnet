using LazyCache;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ViewAttachmentsModel : AuthenticatedPageModel
    {
        [BindProperty(SupportsGet = true)]
        public int UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        public List<AttachmentPreviewDto> Attachments { get; private set; } = new();

        public string? PageUsername { get; private set; }

        public int TotalCount { get; private set; }

        public int PAGE_SIZE => 20;

        public ViewAttachmentsModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache,  CommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {

        }

        public Task<IActionResult> OnGet()
            => WithRegisteredUser(async (_) =>
            {
                if (UserId <= Constants.ANONYMOUS_USER_ID)
                {
                    ModelState.AddModelError(nameof(PageUsername), LanguageProvider.Errors[GetLanguage(), "ERROR_NOT_FOUND", Casing.FirstUpper]);
                    return Page();
                }

                if (PageNum < 1)
                {
                    ModelState.AddModelError(nameof(PageUsername), LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN", Casing.FirstUpper]);
                    return Page();
                }

                var attachmentsTask = (
                    from a in Context.PhpbbAttachments.AsNoTracking()
                    where a.PosterId == UserId

                    join p in Context.PhpbbPosts.AsNoTracking()
                    on a.PostMsgId equals p.PostId
                    into joinedPosts

                    from jp in joinedPosts.DefaultIfEmpty()

                    join t in Context.PhpbbTopics.AsNoTracking()
                    on jp.TopicId equals t.TopicId
                    into joinedTopics

                    from jt in joinedTopics.DefaultIfEmpty()
                    orderby a.Filetime descending
                    select new AttachmentPreviewDto
                    {
                        Id = a.AttachId,
                        PhysicalFilename = a.PhysicalFilename,
                        RealFilename = a.RealFilename,
                        Mimetype = a.Mimetype,
                        FileSize = a.Filesize,
                        FileTime = a.Filetime,
                        ForumId = jp == null ? null : jp.ForumId,
                        TopicTitle = jt == null ? null : jt.TopicTitle
                    }).Skip((PageNum - 1) * PAGE_SIZE).Take(PAGE_SIZE).ToListAsync();
                var countTask = Context.PhpbbAttachments.AsNoTracking().Where(a => a.PosterId == UserId).CountAsync(_ => true);
                var usernameTask = Context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);

                await Task.WhenAll(attachmentsTask, countTask, usernameTask);

                Attachments = await attachmentsTask;
                PageUsername = (await usernameTask).Username;
                TotalCount = await countTask;

                if (string.IsNullOrWhiteSpace(PageUsername))
                {
                    ModelState.AddModelError(nameof(PageUsername), LanguageProvider.Errors[GetLanguage(), "ERROR_NOT_FOUND", Casing.FirstUpper]);
                }

                return Page();
            });
    }
}
