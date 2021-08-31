using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
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

        public FileModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, StorageService storageService,
            IConfiguration config, AnonymousSessionCounter sessionCounter, CommonUtils utils, FileExtensionContentTypeProvider contentTypeProvider, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, config, sessionCounter, utils, languageProvider)
        {
            _storageService = storageService;
            _contentTypeProvider = contentTypeProvider;
        }

        public async Task<IActionResult> OnGet(int id, bool preview = false, Guid? correlationId = null)
        {
            if (correlationId.HasValue && !preview)
            {
                var dto = await Cache.GetAsync<AttachmentDto>(Utils.GetAttachmentCacheKey(id, correlationId.Value));
                if (dto != null)
                {
                    return SendToClient(dto.PhysicalFileName, dto.DisplayName, dto.MimeType, FileType.Attachment);
                }
            }

            var connection = await Context.GetDbConnectionAsync();

            var file = await connection.QuerySingleOrDefaultAsync<AttachmentCheckDto>(
                @"SELECT a.physical_filename, a.real_filename, a.mimetype, p.forum_id, p.post_id 
                    FROM phpbb_attachments a 
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id 
                   WHERE attach_id = @id",
                new { id }
            );

            if (file == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }

            if (preview && file.PostId == null && file.ForumId == null)
            {
                return SendToClient(file.PhysicalFilename, file.RealFilename, file.Mimetype, FileType.Attachment);
            }

            if (!correlationId.HasValue)
            {
                await connection.ExecuteAsync("UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id = @id", new { id });
            }

            return await WithValidForum(
                file.ForumId ?? 0,
                _ => Task.FromResult(SendToClient(file.PhysicalFilename, file.RealFilename, file.Mimetype, FileType.Attachment))
            );
        }

        public async Task<IActionResult> OnGetAvatar(int userId, Guid? correlationId = null)
        {
            string file;
            string getActualFileName(string fileName)
                => $"{Config.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(fileName)}";

            if (correlationId.HasValue)
            {
                file = await Cache.GetAsync<string>(Utils.GetAvatarCacheKey(userId, correlationId.Value));
                if (file != null)
                {
                    file = getActualFileName(file);
                    return SendToClient(file, file, null, FileType.Avatar);
                }
            }

            var connection = await Context.GetDbConnectionAsync();
            file = await connection.QueryFirstOrDefaultAsync<string>("SELECT user_avatar FROM phpbb_users WHERE user_id = @userId", new { userId });
            if (file == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            file = getActualFileName(file);

            return SendToClient(file, file, null, FileType.Avatar);
        }

        public async Task<IActionResult> OnGetDeletedFile(int id, Guid correlationId)
        {
            try
            {
                var file = (await Cache.GetAsync<AttachmentDto>(Utils.GetAttachmentCacheKey(id, correlationId))) ?? throw new InvalidOperationException($"File '{id}' does not exist.");
                return SendToClient(file.PhysicalFileName, file.DisplayName, file.MimeType, FileType.Attachment);
            }
            catch (Exception ex)
            {
                Utils.HandleErrorAsWarning(ex, "Error displaying a deleted attachment");
                return RedirectToPage("Error", new { isNotFound = true });
            }
        }

        private IActionResult SendToClient(string physicalFileName, string realFileName, string mimeType, FileType fileType)
        {
            mimeType ??= (_contentTypeProvider.Mappings.TryGetValue(Path.GetExtension(realFileName), out var val) ? val : null) ?? "application/octet-stream";

            var path = _storageService.GetFilePath(physicalFileName, fileType);
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