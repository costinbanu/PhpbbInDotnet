using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
	public class FileModel : AuthenticatedPageModel
    {
        private readonly IStorageService _storageService;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;
        private readonly IDistributedCache _cache;
        private readonly ILogger _logger;

        public FileModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, 
            IStorageService storageService, FileExtensionContentTypeProvider contentTypeProvider, IConfiguration config, IDistributedCache cache, ILogger logger)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            _storageService = storageService;
            _contentTypeProvider = contentTypeProvider;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IActionResult> OnGet(int id, bool preview = false, Guid? correlationId = null)
        {
            if (correlationId.HasValue && !preview)
            {
                var bytes = await _cache.GetAsync(CacheUtility.GetAttachmentCacheKey(id, correlationId.Value));
                if (bytes?.Length > 0)
                {
                    var dto = await CompressionUtility.DecompressObject<AttachmentDto>(bytes);
                    if (dto is not null)
                    {
                        return await WithValidForum(
                            dto.ForumId,
                            _ => SendToClient(dto.PhysicalFileName!, dto.DisplayName!, dto.MimeType, FileType.Attachment));
                    }
                }
            }

            var file = await SqlExecuter.QuerySingleOrDefaultAsync<AttachmentPreviewDto>(
                @"SELECT a.physical_filename, a.real_filename, a.mimetype, p.forum_id, p.post_id 
                    FROM phpbb_attachments a 
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id 
                   WHERE attach_id = @id",
                new { id });

            if (file == null)
            {
                return NotFound();
            }

            if (preview && file.PostId == null && file.ForumId == null)
            {
                return await SendToClient(file.PhysicalFilename!, file.RealFilename!, file.Mimetype, FileType.Attachment);
            }

            if (!correlationId.HasValue)
            {
                try
                {
                    await SqlExecuter.ExecuteAsyncWithoutResiliency(
                        "UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id = @id",
                        new { id },
                        commandTimeout: 10);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to increment attachment download count for attach ID {id}", id);
                }
            }

            return await WithValidForum(
                file.ForumId ?? 0,
                _ => SendToClient(file.PhysicalFilename!, file.RealFilename!, file.Mimetype, FileType.Attachment));
        }

        public async Task<IActionResult> OnGetAvatar(int userId, Guid? correlationId = null)
        {
            string? file;
            string getActualFileName(string fileName)
                => $"{Configuration.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(fileName)}";

            if (correlationId.HasValue)
            {
                file = await _cache.GetStringAsync(CacheUtility.GetAvatarCacheKey(userId, correlationId.Value));
                if (file != null)
                {
                    file = getActualFileName(file);
                    return await SendToClient(file, file, null, FileType.Avatar);
                }
            }

            file = await SqlExecuter.QueryFirstOrDefaultAsync<string>("SELECT user_avatar FROM phpbb_users WHERE user_id = @userId", new { userId });
            if (file == null)
            {
                return NotFound();
            }
            file = getActualFileName(file);

            return await SendToClient(file, file, null, FileType.Avatar);
        }

        public async Task<IActionResult> OnGetDeletedFile(int id, Guid correlationId)
        {
            try
            {
                var bytes = await _cache.GetAsync(CacheUtility.GetAttachmentCacheKey(id, correlationId));
                if (bytes?.Length > 0)
                {
                    var file = await CompressionUtility.DecompressObject<AttachmentDto>(bytes);
                    if (file is not null)
                    {
                        return await SendToClient(file.PhysicalFileName!, file.DisplayName!, file.MimeType, FileType.Attachment);
                    }
                }
                throw new InvalidOperationException($"File '{id}' does not exist.");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error displaying a deleted attachment");
                return NotFound();
            }
        }

        private async Task<IActionResult> SendToClient(string physicalFileName, string realFileName, string? mimeType, FileType fileType)
        {
            mimeType ??= (_contentTypeProvider.Mappings.TryGetValue(Path.GetExtension(realFileName), out var val) ? val : null) ?? "application/octet-stream";

            var stream = await _storageService.GetFileStream(physicalFileName, fileType);
            if (stream is not null)
            {
                var cd = new ContentDisposition
                {
                    FileName = HttpUtility.UrlEncode(realFileName),
                    Inline = StringUtility.IsMimeTypeInline(mimeType)
                };
                Response.Headers.ContentDisposition = cd.ToString();
                Response.Headers.XContentTypeOptions = "nosniff";
                return File(stream, mimeType);
            }
            else
            {
                return NotFound();
            }
        }
    }
}