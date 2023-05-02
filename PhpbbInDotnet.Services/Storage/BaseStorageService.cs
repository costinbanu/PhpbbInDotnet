using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using StorageOptions = PhpbbInDotnet.Objects.Configuration.Storage;

namespace PhpbbInDotnet.Services.Storage
{
	abstract class BaseStorageService : IStorageService
	{
		private readonly ISqlExecuter _sqlExecuter;

		protected readonly StorageOptions StorageOptions;
		protected readonly IConfiguration Configuration;

		protected abstract string AttachmentsPath { get; }
		protected abstract string AvatarsPath { get; }
		protected abstract string EmojiPath { get; }

		public BaseStorageService(IConfiguration config, ISqlExecuter sqlExecuter)
		{
			_sqlExecuter = sqlExecuter;
			StorageOptions = config.GetObject<StorageOptions>();
			Configuration = config;
		}

		public string? GetEmojiRelativeUrl(string name)
			=> CombineToRelativePath(".", StorageOptions.Emojis!, name);

		public abstract Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId);
		public abstract Task<(IEnumerable<string> Succeeded, IEnumerable<string> Failed)> BulkDeleteAttachments(IEnumerable<string> files);
		public abstract Task<bool> DeleteAvatar(int userId, string fileName);
		public abstract Task<bool> DeleteAttachment(string name);
		public abstract Task<string?> DuplicateAttachment(PhpbbAttachments attachment, int userId);
		public abstract Task<Stream?> GetFileStream(string name, FileType fileType);
		public abstract Task<DateTime?> GetLastWriteTime(string path);
		public abstract Task<bool> UploadAvatar(int userId, Stream contents, string fileName);
		public abstract Task<bool> UpsertEmoji(string name, Stream file);
		public abstract Task WriteAllTextToFile(string path, string contents);

		protected Task<PhpbbAttachments> AddToDatabase(string uploadedFileName, string physicalFileName, long fileSize, string mimeType, int posterId)
			=> _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbAttachments>(
				"INSERT INTO phpbb_attachments (attach_comment, extension, filetime, filesize, mimetype, physical_filename, real_filename, poster_id) " +
				"VALUES ('', @Extension, @Filetime, @Filesize, @Mimetype, @PhysicalFilename, @RealFilename, @PosterId); " +
				"SELECT * FROM phpbb_attachments WHERE attach_id = LAST_INSERT_ID()",
				new
				{
					Extension = Path.GetExtension(uploadedFileName).Trim('.').ToLowerInvariant(),
					Filetime = DateTime.UtcNow.ToUnixTimestamp(),
					fileSize,
					mimeType,
					physicalFileName,
					RealFilename = Path.GetFileName(uploadedFileName),
					posterId,
				});

		protected static string GenerateNewAttachmentFileName(int userId)
			=> $"{userId}_{Guid.NewGuid():n}";

		protected string GetAvatarPhysicalFileName(int userId, string originalFileName)
			=> $"{Configuration.GetValue<string>("AvatarSalt")}_{userId}.{Path.GetExtension(originalFileName).TrimStart('.')}";

		protected static string CombineToRelativePath(params string[] parts)
		{
			if (parts.Length == 0)
			{
				return string.Empty;
			}
			var sb = new StringBuilder();
			foreach (var part in parts )
			{
				sb.Append(part.Trim().Trim('/').Trim('\\')).Append('/');
			}
			return sb.ToString();
		}
	}
}
