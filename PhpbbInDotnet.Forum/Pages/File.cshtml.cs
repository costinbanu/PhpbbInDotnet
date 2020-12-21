using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web;
using IOFile = System.IO.File;

namespace PhpbbInDotnet.Forum.Pages
{
    public class FileModel : AuthenticatedPageModel
    {
        private readonly StorageService _storageService;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public FileModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, StorageService storageService, 
            IConfiguration config, AnonymousSessionCounter sessionCounter, CommonUtils utils, FileExtensionContentTypeProvider contentTypeProvider, LanguageProvider languageProvider)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils, languageProvider)
        {
            _storageService = storageService;
            _contentTypeProvider = contentTypeProvider;
        }

        public async Task<IActionResult> OnGet(int Id)
        {
            var connection = Context.Database.GetDbConnection();
   
            var file = await connection.QuerySingleOrDefaultAsync("SELECT a.physical_filename, a.real_filename, a.mimetype, p.forum_id FROM phpbb_attachments a JOIN phpbb_posts p on a.post_msg_id = p.post_id WHERE attach_id = @Id", new { Id });
            if (file == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            uint? forumId = file?.forum_id;
            string physicalFilename = file?.physical_filename;
            string realFilename = file?.real_filename;
            string mimeType = file?.mimetype;
            await connection.ExecuteAsync("UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id = @Id", new { Id });
            
            return await WithValidForum(unchecked((int)(forumId ?? 0)), async (_) => await Task.FromResult(SendToClient(physicalFilename, realFilename, mimeType, false)));
        }

        public IActionResult OnGetPreview(string physicalFileName, string realFileName, string mimeType)
            => SendToClient(physicalFileName, realFileName, mimeType, false);

        public async Task<IActionResult> OnGetAvatar(int userId)
        {
            string file;
            var connection = Context.Database.GetDbConnection();

            file = await connection.QueryFirstOrDefaultAsync<string>("SELECT user_avatar FROM phpbb_users WHERE user_id = @userId", new { userId });
            if (file == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            file = $"{Config.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(file)}";

            return SendToClient(file, file, null, true);
        }

        private IActionResult SendToClient(string physicalFileName, string realFileName, string mimeType, bool isAvatar)
        {
            mimeType ??= (_contentTypeProvider.Mappings.TryGetValue(Path.GetExtension(realFileName), out var val) ? val : null) ?? "application/octet-stream";

            var path = _storageService.GetFilePath(physicalFileName, isAvatar);
            if (IOFile.Exists(path))
            {
                var cd = new ContentDisposition
                {
                    FileName = HttpUtility.UrlEncode(realFileName),
                    Inline = mimeType.IsMimeTypeInline()
                };
                Response.Headers.Add("Content-Disposition", cd.ToString());
                Response.Headers.Add("X-Content-Type-Options", "nosniff");
                return File(IOFile.OpenRead(path), mimeType);
            }
            else
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
        }
    }
}