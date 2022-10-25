using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
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
        private readonly IStorageService _storageService;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;
        private readonly IConfiguration _config;

        public FileModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache, IStorageService storageService,
            IConfiguration config, ILogger logger, FileExtensionContentTypeProvider contentTypeProvider, ITranslationProvider translationProvider)
            : base(context, forumService, userService, cache, logger, translationProvider)
        {
            _storageService = storageService;
            _contentTypeProvider = contentTypeProvider;
            _config = config;
        }

        public async Task<IActionResult> OnGet(int id, bool preview = false, Guid? correlationId = null)
        {
            if (correlationId.HasValue && !preview)
            {
                var dto = await Cache.GetAsync<AttachmentDto>(CacheUtility.GetAttachmentCacheKey(id, correlationId.Value));
                if (dto != null)
                {
                    return await WithValidForum(
                        dto.ForumId,
                        _ => Task.FromResult(SendToClient(dto.PhysicalFileName!, dto.DisplayName!, dto.MimeType, FileType.Attachment)));
                }
            }

            var sqlExecuter = Context.GetSqlExecuter();

            var file = await sqlExecuter.QuerySingleOrDefaultAsync<AttachmentPreviewDto>(
                @"SELECT a.physical_filename, a.real_filename, a.mimetype, p.forum_id, p.post_id 
                    FROM phpbb_attachments a 
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id 
                   WHERE attach_id = @id",
                new { id }
            );

            if (file == null)
            {
                return NotFound();
            }

            if (preview && file.PostId == null && file.ForumId == null)
            {
                return SendToClient(file.PhysicalFilename!, file.RealFilename!, file.Mimetype, FileType.Attachment);
            }

            if (!correlationId.HasValue)
            {
                await sqlExecuter.ExecuteAsync("UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id = @id", new { id });
            }

            return await WithValidForum(
                file.ForumId ?? 0,
                _ => Task.FromResult(SendToClient(file.PhysicalFilename!, file.RealFilename!, file.Mimetype, FileType.Attachment)));
        }

        public async Task<IActionResult> OnGetAvatar(int userId, Guid? correlationId = null)
        {
            string file;
            string getActualFileName(string fileName)
                => $"{_config.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(fileName)}";

            if (correlationId.HasValue)
            {
                file = await Cache.GetAsync<string>(CacheUtility.GetAvatarCacheKey(userId, correlationId.Value));
                if (file != null)
                {
                    file = getActualFileName(file);
                    return SendToClient(file, file, null, FileType.Avatar);
                }
            }

            var sqlExecuter = Context.GetSqlExecuter();
            file = await sqlExecuter.QueryFirstOrDefaultAsync<string>("SELECT user_avatar FROM phpbb_users WHERE user_id = @userId", new { userId });
            if (file == null)
            {
                return NotFound();
            }
            file = getActualFileName(file);

            return SendToClient(file, file, null, FileType.Avatar);
        }

        public async Task<IActionResult> OnGetDeletedFile(int id, Guid correlationId)
        {
            try
            {
                var file = (await Cache.GetAsync<AttachmentDto>(CacheUtility.GetAttachmentCacheKey(id, correlationId))) ?? throw new InvalidOperationException($"File '{id}' does not exist.");
                return SendToClient(file.PhysicalFileName!, file.DisplayName!, file.MimeType, FileType.Attachment);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error displaying a deleted attachment");
                return NotFound();
            }
        }

        private IActionResult SendToClient(string physicalFileName, string realFileName, string? mimeType, FileType fileType)
        {
            mimeType ??= (_contentTypeProvider.Mappings.TryGetValue(Path.GetExtension(realFileName), out var val) ? val : null) ?? "application/octet-stream";

            var path = _storageService.GetFilePath(physicalFileName, fileType);
            if (IOFile.Exists(path))
            {
                var cd = new ContentDisposition
                {
                    FileName = HttpUtility.UrlEncode(realFileName),
                    Inline = StringUtility.IsMimeTypeInline(mimeType)
                };
                Response.Headers.Add("Content-Disposition", cd.ToString());
                Response.Headers.Add("X-Content-Type-Options", "nosniff");
                return File(IOFile.OpenRead(path!), mimeType);
            }
            else
            {
                return NotFound();
            }
        }
    }
}