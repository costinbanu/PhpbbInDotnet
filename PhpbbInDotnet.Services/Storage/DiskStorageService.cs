using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Storage
{
	class DiskStorageService : BaseStorageService
	{
		private readonly IConfiguration _config;
		private readonly IWebHostEnvironment _environment;
		private readonly ILogger _logger;

		public DiskStorageService(IConfiguration config, IWebHostEnvironment environment, ISqlExecuter sqlExecuter, ILogger logger)
			: base(config, sqlExecuter)
		{
			_config = config;
			_environment = environment;
			_logger = logger;
		}

		protected override string AttachmentsPath => Path.Combine(_environment.WebRootPath, StorageOptions.Files!);
		protected override string AvatarsPath => Path.Combine(_environment.WebRootPath, StorageOptions.Avatars!);
		protected override string EmojiPath => Path.Combine(_environment.WebRootPath, StorageOptions.Emojis!);

		public override async Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
		{
			var succeeded = new List<PhpbbAttachments>();
			var failed = new List<string>();

			foreach (var file in attachedFiles)
			{
				try
				{
					var name = GenerateNewAttachmentFileName(userId);
					using (var input = file.OpenReadStream())
					using (var fs = File.Open(Path.Combine(AttachmentsPath, name), FileMode.Create))
					{
						await input.CopyToAsync(fs);
					}

					succeeded.Add(await AddToDatabase(file.FileName, name, file.Length, file.ContentType, userId));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error uploading attachment by user {id}.", userId);
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
				_logger.Error(ex);
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
				_logger.Error(ex, "Error uploading avatar.");
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
					_logger.Error(ex, "Error deleting file '{file}'.", file);
					failed.Add(file);
				}
			}
			return (succeeded, failed);
		}

		public override async Task<bool> UpsertEmoji(string name, Stream file)
		{
			try
			{
				using var fs = File.OpenWrite(Path.Combine(EmojiPath, name));
				await file.CopyToAsync(fs);
				await fs.FlushAsync();
				return true;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error uploading emoji '{name}'", name);
				return false;
			}
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

		private string GetFilePath(string name, FileType fileType)
			=> fileType switch
			{
				FileType.Attachment => Path.Combine(AttachmentsPath, name),
				FileType.Avatar => Path.Combine(AvatarsPath, name),
				FileType.Emoji => Path.Combine(EmojiPath, name),
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
				_logger.Error(ex, "Error deleting file '{name}'.", name);
			}
			return false;
		}
	}
}
