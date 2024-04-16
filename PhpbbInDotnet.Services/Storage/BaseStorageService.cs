using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using StorageOptions = PhpbbInDotnet.Objects.Configuration.Storage;

namespace PhpbbInDotnet.Services.Storage
{
    abstract class BaseStorageService : IStorageService
	{
		private readonly ISqlExecuter _sqlExecuter;
		private readonly string _emojiPath;

		protected readonly StorageOptions StorageOptions;
		protected readonly IConfiguration Configuration;
		protected readonly IWebHostEnvironment Environment;
		protected readonly ILogger Logger;

		protected abstract string AttachmentsPath { get; }
		protected abstract string AvatarsPath { get; }

		public BaseStorageService(IConfiguration config, ISqlExecuter sqlExecuter, IWebHostEnvironment environment, ILogger logger)
		{
			_sqlExecuter = sqlExecuter;
			StorageOptions = config.GetObject<StorageOptions>();
			Configuration = config;
			Environment = environment;
			Logger = logger;
			_emojiPath = Path.Combine(environment.WebRootPath, StorageOptions.Emojis!);
		}

		public string? GetEmojiRelativeUrl(string name)
			=> CombineToRelativePath(".", StorageOptions.Emojis!, name);

		public async Task<bool> UpsertEmoji(string name, Stream file)
		{
			try
			{
				using var fs = File.OpenWrite(Path.Combine(_emojiPath, name));
				await file.CopyToAsync(fs);
				await fs.FlushAsync();
				return true;
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Error uploading emoji '{name}'", name);
				return false;
			}
		}

		public async Task<bool> DeleteEmoji(string name)
		{
			try
			{
				await Task.Run(() => File.Delete(Path.Combine(_emojiPath, name)));
				return true;
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Error uploading emoji '{name}'", name);
				return false;
			}
		}

		public abstract Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId);
		public abstract Task<(IEnumerable<string> Succeeded, IEnumerable<string> Failed)> BulkDeleteAttachments(IEnumerable<string> files);
		public abstract Task<bool> DeleteAvatar(int userId, string fileName);
		public abstract Task<bool> DeleteAttachment(string name);
		public abstract Task<string?> DuplicateAttachment(PhpbbAttachments attachment, int userId);
		public abstract Task<Stream?> GetFileStream(string name, FileType fileType);
		public abstract Task<DateTime?> GetLastWriteTime(string path);
		public abstract Task<bool> UploadAvatar(int userId, Stream contents, string fileName);
		public abstract Task WriteAllTextToFile(string path, string contents);
        public abstract Task<List<(DateTime LogDate, string? LogPath)>?> GetSystemLogs();

        protected Task<PhpbbAttachments> AddToDatabase(string uploadedFileName, string physicalFileName, long fileSize, string mimeType, int posterId)
			=> _sqlExecuter.QuerySingleAsync<PhpbbAttachments>(
				@$"INSERT INTO phpbb_attachments (attach_comment, extension, filetime, filesize, mimetype, physical_filename, real_filename, poster_id) 
				   VALUES ('', @Extension, @Filetime, @Filesize, @Mimetype, @PhysicalFilename, @RealFilename, @PosterId);
				   SELECT * FROM phpbb_attachments WHERE attach_id = {_sqlExecuter.LastInsertedItemId}",
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
			for (var i = 0; i < parts.Length; i++)
			{
				sb.Append(parts[i].Trim().Trim('/').Trim('\\'));
				if (i < parts.Length - 1)
				{
					sb.Append('/');
				}
			}
			return sb.ToString();
		}

        protected (DateTime LogDate, string? LogPath) ParseLogName(string path)
        {
            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path)[3..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return (date, path);
            }
            return (default, default);
        }
    }
}
