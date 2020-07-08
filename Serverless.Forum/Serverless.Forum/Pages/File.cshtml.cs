using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.IO;
using System.Threading.Tasks;
using IOFile = System.IO.File;

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
            uint? forumId = null;
            string physicalFilename = null;
            string realFilename = null;
            string mimeType = null;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                var file = await connection.QuerySingleOrDefaultAsync("SELECT a.physical_filename, a.real_filename, a.mimetype, p.forum_id FROM phpbb_attachments a JOIN phpbb_posts p on a.post_msg_id = p.post_id WHERE attach_id = @Id", new { Id });
                if (file == null)
                {
                    return NotFound();
                }
                forumId = file?.forum_id;
                physicalFilename = file?.physical_filename;
                realFilename = file?.real_filename;
                mimeType = file?.mimetype;
                await connection.ExecuteAsync("UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id = @Id", new { Id });
            }

            return await WithValidForum(unchecked((int)(forumId ?? 0)), async (_) => await Task.FromResult(SendToClient(physicalFilename, realFilename, mimeType, false)));
        }

        public IActionResult OnGetPreview(string physicalFileName, string realFileName, string mimeType)
            => SendToClient(physicalFileName, realFileName, mimeType, false);

        public async Task<IActionResult> OnGetAvatar(int userId)
        {
            string file;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                file = await connection.QuerySingleOrDefaultAsync<string>("SELECT user_avatar FROM phpbb_users WHERE user_id = @userId", new { userId });
            }

            if (file == null)
            {
                return NotFound();
            }

            return SendToClient(file, file, null, true);
        }

        private IActionResult SendToClient(string physicalFileName, string realFileName, string mimeType, bool isAvatar)
        {
            mimeType ??= new FileExtensionContentTypeProvider().Mappings[Path.GetExtension(realFileName)];

            //TODO: this doesn't work in chrome
            //var header = $"{(mimeType.IsMimeTypeInline() ? "inline" : "attachment")}; " +
            //    $"filename={Uri.EscapeDataString(displayName)}; " +
            //    $"filename*=UTF-8''{Uri.EscapeDataString(displayName)}";

            //return Redirect(_storageService.GetFileUrl(physicalFileName, isAvatar /*header, mimeType*/));

            var path = _storageService.GetFilePath(physicalFileName, isAvatar);
            if (IOFile.Exists(path))
            {
                return File(IOFile.OpenRead(path), mimeType, realFileName);
            }
            else
            {
                return NotFound("Fișierul solicitat nu există");
            }
        }
    }
}