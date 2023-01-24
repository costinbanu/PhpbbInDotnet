using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
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

        public Paginator? Paginator { get; private set; }

        const int PAGE_SIZE = 20;

        public ViewAttachmentsModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache,  ILogger logger, ITranslationProvider translationProvider)
            : base(context, forumService, userService, cache, logger, translationProvider)
        {

        }

        public Task<IActionResult> OnGet()
            => WithRegisteredUser(async (_) =>
            {
                PhpbbUsers? user = null;
                PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                await ResiliencyUtility.RetryOnceAsync(
                    toDo: async () =>
                    {
                        var restrictedForums = (await ForumService.GetRestrictedForumList(ForumUser)).Select(f => f.forumId);
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
                            where !restrictedForums.Contains(jt.ForumId)
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
                                PostId = jp == null ? null : jp.PostId,
                                TopicTitle = jt == null ? null : jt.TopicTitle
                            }).Skip((PageNum - 1) * PAGE_SIZE).Take(PAGE_SIZE).ToListAsync();
                        var countTask = Context.PhpbbAttachments.AsNoTracking().Where(a => a.PosterId == UserId).CountAsync(_ => true);
                        var userTask = Context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);

                        await Task.WhenAll(attachmentsTask, countTask, userTask);

                        Attachments = await attachmentsTask;
                        user = await userTask;
                        Paginator = new Paginator(await countTask, PageNum, $"ViewAttachments?userId={UserId}", PAGE_SIZE, "pageNum");
                    },
                    evaluateSuccess: () => user is null || (Attachments!.Count > 0 && PageNum == Paginator!.CurrentPage),
                    fix: () => PageNum = Paginator!.CurrentPage);

                if (user is null)
                {
                    return NotFound();
                }
                else
                {
                    PageUsername = user.Username;
                }

                return Page();
            });
    }
}
