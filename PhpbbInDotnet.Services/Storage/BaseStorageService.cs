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
using System.Threading.Tasks;
using StorageOptions = PhpbbInDotnet.Objects.Configuration.Storage;

namespace PhpbbInDotnet.Services.Storage
{
	abstract class BaseStorageService : IStorageService
	{
		protected readonly ISqlExecuter _sqlExecuter;
		protected readonly StorageOptions StorageOptions;
		protected readonly string AttachmentsPath;
		protected readonly string AvatarsPath;
		protected readonly string EmojiPath;

		public BaseStorageService(IConfiguration config, IWebHostEnvironment environment, ISqlExecuter sqlExecuter)
		{
			_sqlExecuter = sqlExecuter;
			StorageOptions = config.GetObject<StorageOptions>();
			AttachmentsPath = Path.Combine(environment.WebRootPath, StorageOptions.Files!);
			AvatarsPath = Path.Combine(environment.WebRootPath, StorageOptions.Avatars!);
			EmojiPath = Path.Combine(environment.WebRootPath, StorageOptions.Emojis!);
		}

		public string? GetFileUrl(string name, FileType fileType)
			=> fileType switch
			{
				FileType.Attachment => $"./{StorageOptions.Files!.Trim('/')}/{name.TrimStart('/')}",
				FileType.Avatar => $"./{StorageOptions.Avatars!.Trim('/')}/{name.TrimStart('/')}",
				FileType.Emoji => $"./{StorageOptions.Emojis!.Trim('/')}/{name.TrimStart('/')}",
				_ => null
			};

		public string? GetFilePath(string name, FileType fileType)
			=> fileType switch
			{
				FileType.Attachment => Path.Combine(AttachmentsPath, name),
				FileType.Avatar => Path.Combine(AvatarsPath, name),
				FileType.Emoji => Path.Combine(EmojiPath, name),
				_ => null
			};

		public abstract Task<(List<PhpbbAttachments> SucceededUploads, List<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId);
		public abstract (IEnumerable<string> Succeeded, IEnumerable<string> Failed) BulkDeleteAttachments(IEnumerable<string> files);
		public abstract bool DeleteAvatar(int userId, string extension);
		public abstract bool DeleteFile(string? name, bool isAvatar);
		public abstract string? DuplicateFile(PhpbbAttachments attachment, int userId);
		public abstract Task<byte[]> GetAttachmentContents(string name);
		public abstract Task<bool> UploadAvatar(int userId, Stream contents, string fileName);
		public abstract Task<bool> UpsertEmoji(string name, Stream file);
		public abstract void WriteAllTextToFile(string path, string contents);

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
	}
}
