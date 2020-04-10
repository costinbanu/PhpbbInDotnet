using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class FileModel : ModelWithLoggedUser
    {
        private readonly StorageService _storageService;

        public FileModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService, StorageService storageService) 
            : base(config, utils, forumService, userService, cacheService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> OnGet(int Id)
        {
            using (var context = new ForumDbContext(_config))
            {
                var file = await context.PhpbbAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.AttachId == Id);

                if (file == null)
                {
                    return NotFound();
                }

                var forum = await (
                    from f in context.PhpbbForums.AsNoTracking()

                    join t in context.PhpbbTopics.AsNoTracking()
                    on f.ForumId equals t.ForumId

                    join p in context.PhpbbPosts.AsNoTracking()
                    on t.TopicId equals p.TopicId

                    where p.PostId == file.PostMsgId

                    select f
                ).FirstOrDefaultAsync();

                var response = await ValidateForumPermissionsResponsesAsync(forum, forum?.ForumId ?? 0);

                if (response != null)
                {
                    return response;
                }

                return await SendToClient(file.PhysicalFilename, file.RealFilename, file.Mimetype);
            }
        }

        public async Task<IActionResult> OnGetAvatar(int userId)
        {
            using (var context = new ForumDbContext(_config))
            {
                var file = await (from u in context.PhpbbUsers.AsNoTracking()
                                  where u.UserId == userId
                                  select u.UserAvatar).FirstOrDefaultAsync();

                if (file == null)
                {
                    return NotFound();
                }

                return await SendToClient($"avatars/{userId}{Path.GetExtension(file)}", file);
            }
        }

        private async Task<IActionResult> SendToClient(string fileName, string displayName, string mimeType = null)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                mimeType = new FileExtensionContentTypeProvider().Mappings[Path.GetExtension(fileName)];
            }

            //TODO: this doesn't work in chrome
            var header = $"{(mimeType.IsMimeTypeInline() ? "inline" : "attachment")}; " +
                $"filename={Uri.EscapeDataString(displayName)}; " +
                $"filename*=UTF-8''{Uri.EscapeDataString(displayName)}";

            return Redirect(await _storageService.GetFileUrl(fileName, header, mimeType));
        }
    }
}