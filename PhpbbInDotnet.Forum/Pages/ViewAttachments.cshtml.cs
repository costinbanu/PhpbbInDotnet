using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
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

        public ViewAttachmentsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, 
            IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
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
                        var restrictedForums = (await ForumService.GetRestrictedForumList(ForumUser)).Select(f => f.forumId).DefaultIfEmpty();
                        var attachmentsTask = SqlExecuter.WithPagination((PageNum - 1) * PAGE_SIZE, PAGE_SIZE).QueryAsync<AttachmentPreviewDto>(
                            @"SELECT a.attach_id, a.physical_filename, a.real_filename, a.mimetype, a.filesize, a.filetime, p.forum_id, p.post_id, t.topic_title
                                FROM phpbb_attachments a
                                LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
                                LEFT JOIN phpbb_topics t ON p.topic_id = t.topic_id
                               WHERE a.poster_id = @userId AND t.forum_id NOT IN @restrictedForums
                               ORDER BY a.filetime DESC",
                            new
                            {
                                UserId,
                                restrictedForums
                            });

                        var countTask = SqlExecuter.ExecuteScalarAsync<int>(
                            "SELECT count(1) FROM phpbb_attachments WHERE poster_id = @userId",
                            new { UserId });
                        var userTask = SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                            "SELECT * FROM phpbb_users WHERE user_id = @userId",
                            new { UserId });

                        await Task.WhenAll(attachmentsTask, countTask, userTask);

                        Attachments = (await attachmentsTask).AsList();
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
