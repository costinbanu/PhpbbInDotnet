using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
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

        public FileModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, StorageService storageService)
            : base(context, forumService, userService, cacheService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> OnGet(int Id)
        {
            dynamic file;
            int? forumId;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                file = await connection.QuerySingleAsync("SELECT physical_filename, post_msg_id FROM phpbb_attachments WHERE attach_id = @Id", new { Id });
                if (file == null)
                {
                    return NotFound();
                }
                forumId = await connection.QuerySingleAsync<int?>("SELECT forum_id FROM phpbb_posts WHERE post_id = @PostMsgId", new { file.PostMsgId });
            }

            return await WithValidForum(forumId ?? 0, async (_) => await SendToClient(file.PhysicalFilename, false));
        }

        public async Task<IActionResult> OnGetPreview(string physicalFileName/*, string realFileName, string mimeType*/)
            => await SendToClient(physicalFileName, false);

        public async Task<IActionResult> OnGetAvatar(int userId)
        {
            string file;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                file = await connection.QuerySingleAsync<string>("SELECT user_avatar FROM phpbb_users WHERE user_id = @userId", new { userId });
            }

            if (file == null)
            {
                return NotFound();
            }

            return await SendToClient(file, true);
        }

        private async Task<IActionResult> SendToClient(string fileName, bool isAvatar /*string displayName, string mimeType = null*/)
        {
            //if (string.IsNullOrWhiteSpace(mimeType))
            //{
            //    mimeType = new FileExtensionContentTypeProvider().Mappings[Path.GetExtension(fileName)];
            //}

            //TODO: this doesn't work in chrome
            //var header = $"{(mimeType.IsMimeTypeInline() ? "inline" : "attachment")}; " +
            //    $"filename={Uri.EscapeDataString(displayName)}; " +
            //    $"filename*=UTF-8''{Uri.EscapeDataString(displayName)}";

            return Redirect(_storageService.GetFileUrl(fileName, isAvatar /*header, mimeType*/));
        }
    }
}