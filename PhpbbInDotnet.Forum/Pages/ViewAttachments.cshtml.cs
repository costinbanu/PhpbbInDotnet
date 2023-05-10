using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
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
        private readonly IForumDbContext _dbContext;

        public ViewAttachmentsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IForumDbContext dbContext, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _dbContext = dbContext;
        }

        public Task<IActionResult> OnGet()
            => WithRegisteredUser(async (_) =>
            {
                PhpbbUsers? user = null;
                PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                await ResiliencyUtility.RetryOnceAsync(
                    toDo: async () =>
                    {
                        var restrictedForums = (await ForumService.GetRestrictedForumList(ForumUser)).Select(f => f.forumId).DefaultIfEmpty();
                        var attachmentsTask = (
                            from a in _dbContext.PhpbbAttachments.AsNoTracking()
                            where a.PosterId == UserId

                            join p in _dbContext.PhpbbPosts.AsNoTracking()
                            on a.PostMsgId equals p.PostId
                            into joinedPosts

                            from jp in joinedPosts.DefaultIfEmpty()

                            join t in _dbContext.PhpbbTopics.AsNoTracking()
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
                        var countTask = _dbContext.PhpbbAttachments.AsNoTracking().Where(a => a.PosterId == UserId).CountAsync(_ => true);
                        var userTask = _dbContext.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);

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
