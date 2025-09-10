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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Storage
{
    class DiskStorageService : BaseStorageService
	{
		private readonly IConfiguration _config;

		public DiskStorageService(IConfiguration config, IWebHostEnvironment environment, ISqlExecuter sqlExecuter, ILogger logger)
			: base(config, sqlExecuter, environment, logger)
		{
			_config = config;
		}

		protected override string AttachmentsPath => Path.Combine(Environment.WebRootPath, StorageOptions.Files!);
		protected override string AvatarsPath => Path.Combine(Environment.WebRootPath, StorageOptions.Avatars!);

		public override async Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId, int minOrderInPost)
		{
			var succeeded = new List<PhpbbAttachments>();
			var failed = new List<string>();

			foreach (var (file, index) in attachedFiles.OrderBy(f => f.FileName).Indexed())
			{
				try
				{
					var name = GenerateNewAttachmentFileName(userId);
					using (var input = file.OpenReadStream())
					using (var fs = File.Open(Path.Combine(AttachmentsPath, name), FileMode.Create))
					{
						await input.CopyToAsync(fs);
					}

					succeeded.Add(await AddToDatabase(file.FileName, name, file.Length, file.ContentType, userId, index + minOrderInPost));
				}
				catch (Exception ex)
				{
					Logger.Error(ex, "Error uploading attachment by user {id}.", userId);
					failed.Add(file.FileName);
				}
			}

			return (succeeded, failed);
		}

		public override async Task<Stream?> GetFileStream(string name, FileType fileType)
		{
			var path = GetFilePath(name, fileType);
			if (File.Exists(path))
			{
				return await Task.Run(() => File.OpenRead(path));
			}

			return null;
		}

		public override async Task<string?> DuplicateAttachment(PhpbbAttachments attachment, int userId)
		{
			try
			{
				var name = GenerateNewAttachmentFileName(userId);
				await Task.Run(() => File.Copy(Path.Combine(AttachmentsPath, attachment.PhysicalFilename), Path.Combine(AttachmentsPath, name)));
				return name;
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
				return null;
			}
		}

		public override async Task<bool> UploadAvatar(int userId, Stream contents, string fileName)
		{
			try
			{
				var name = $"{_config.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(fileName)}";
				using var fs = File.Open(Path.Combine(AvatarsPath, name), FileMode.Create);
				await contents.CopyToAsync(fs);
				await fs.FlushAsync();
				return true;
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Error uploading avatar.");
				return false;
			}
		}

		public override Task<bool> DeleteAvatar(int userId, string originalFileName)
			=> Task.Run(() => DeleteFile(GetAvatarPhysicalFileName(userId, originalFileName), FileType.Avatar));

		public override Task<bool> DeleteAttachment(string name)
			=> Task.Run(() => DeleteFile(name, FileType.Attachment));

		public override async Task<(IEnumerable<string> Succeeded, IEnumerable<string> Failed)> BulkDeleteAttachments(IEnumerable<string> files)
		{
			var succeeded = new List<string>();
			var failed = new List<string>();
			foreach (var file in files)
			{
				try
				{
					await Task.Run(() => DeleteFile(file, FileType.Attachment));
					succeeded.Add(file);
				}
				catch (Exception ex)
				{
					Logger.Error(ex, "Error deleting file '{file}'.", file);
					failed.Add(file);
				}
			}
			return (succeeded, failed);
		}

		public override Task WriteAllTextToFile(string path, string contents)
			=> Task.Run(() => File.WriteAllText(path, contents));

		public override Task<DateTime?> GetLastWriteTime(string path)
		{
			DateTime? lastRun = null;
			try
			{
				lastRun = new FileInfo(path).LastWriteTimeUtc;
			}
			catch { }

			return Task.FromResult(lastRun);
		}

        public override Task<List<(DateTime LogDate, string? LogPath)>?> GetSystemLogs()
		{
			try
			{
				return Task.FromResult<List<(DateTime LogDate, string? LogPath)>?>(
					(from f in Directory.EnumerateFiles("logs", "log*.txt")
					 let parsed = ParseLogName(f)
					 where parsed.LogDate != default && parsed.LogPath != default
					 orderby parsed.LogDate descending
					 select parsed).ToList());
			}
			catch (Exception ex)
			{
				Logger.Warning(ex);
				return Task.FromResult((List<(DateTime LogDate, string? LogPath)> ?)null);
			}
		}

        private string GetFilePath(string name, FileType fileType)
			=> fileType switch
			{
				FileType.Attachment => Path.Combine(AttachmentsPath, name),
				FileType.Avatar => Path.Combine(AvatarsPath, name),
				FileType.Log => name,
				_ => throw new ArgumentException($"Unknown value '{fileType}'.", nameof(fileType))
			};

		private bool DeleteFile(string name, FileType fileType)
		{
			try
			{
				var path = GetFilePath(name, fileType);
				if (File.Exists(path))
				{
					File.Delete(path);
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Error deleting file '{name}'.", name);
			}
			return false;
		}
	}
}
