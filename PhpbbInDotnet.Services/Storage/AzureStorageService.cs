using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Storage
{
	class AzureStorageService : BaseStorageService
	{
		private readonly IConfiguration _config;
		private readonly BlobContainerClient _blobContainerClient;

		public AzureStorageService(IConfiguration config, ISqlExecuter sqlExecuter, IWebHostEnvironment environment, ILogger logger, BlobContainerClient blobContainerClient)
			: base(config, sqlExecuter, environment, logger)
		{
			_config = config;
			_blobContainerClient = blobContainerClient;
		}

		protected override string AttachmentsPath => StorageOptions.Files!;
		protected override string AvatarsPath =>  StorageOptions.Avatars!;

		public override async Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
		{
			var succeededUploads = new ConcurrentBag<(string uploadedFileName, string physicalFileName, long fileSize, string mimeType)>();
			var failedUploads = new ConcurrentBag<string>();

			await Task.WhenAll(attachedFiles.Select(async file =>
			{
				try
				{
					var name = GenerateNewAttachmentFileName(userId);
					using var source = file.OpenReadStream();
					using var destination = await _blobContainerClient.GetBlobClient(CombineToRelativePath(AttachmentsPath, name)).OpenWriteAsync(overwrite: true);
					await source.CopyToAsync(destination);

					succeededUploads.Add((file.FileName, name, file.Length, file.ContentType));
				}
				catch (Exception ex)
				{
					Logger.Error(ex, "Error uploading attachment by user {id}.", userId);
					failedUploads.Add(file.FileName);
				}
			}));

			var succeededUploadsToReturn = new List<PhpbbAttachments>();
			foreach(var (uploadedFileName, physicalFileName, fileSize, mimeType) in succeededUploads)
			{
				try
				{
					var attachment = await AddToDatabase(uploadedFileName, physicalFileName, fileSize, mimeType, userId);
					succeededUploadsToReturn.Add(attachment);
				}
				catch (Exception ex)
				{
					failedUploads.Add(uploadedFileName);
					Logger.Error(ex, "Error uploading attachment by user {id}.", userId);
				}
			}

			return (succeededUploadsToReturn, failedUploads);
		}

		public override async Task<(IEnumerable<string> Succeeded, IEnumerable<string> Failed)> BulkDeleteAttachments(IEnumerable<string> files)
		{
			var succeeded = new ConcurrentBag<string>();
			var failed = new ConcurrentBag<string>();

			await Task.WhenAll(files.Select(async file =>
			{
				if(await DeleteFile(file, FileType.Attachment))
				{
					succeeded.Add(file);
				}
				else
				{
					failed.Add(file);
				}
			}));

			return (succeeded, failed);
		}

		public override Task<bool> DeleteAvatar(int userId, string originalFileName)
			=> DeleteFile(GetAvatarPhysicalFileName(userId, originalFileName), FileType.Avatar);

		public override Task<bool> DeleteAttachment(string name)
			=> DeleteFile(name, FileType.Attachment);

		public override async Task<string?> DuplicateAttachment(PhpbbAttachments attachment, int userId)
		{
			try
			{
				var newName = GenerateNewAttachmentFileName(userId);
				using var source = await _blobContainerClient.GetBlobClient(CombineToRelativePath(AttachmentsPath, attachment.PhysicalFilename)).OpenReadAsync();
				using var destination = await _blobContainerClient.GetBlobClient(CombineToRelativePath(AttachmentsPath, newName)).OpenWriteAsync(overwrite: true);
				await source.CopyToAsync(destination);
				return newName;
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
				return null;
			}
		}

		public override async Task<Stream?> GetFileStream(string name, FileType fileType)
		{
			var blobClient = _blobContainerClient.GetBlobClient(GetFilePath(name, fileType));
			if (await blobClient.ExistsAsync())
			{
				return await blobClient.OpenReadAsync();
			}
			return null;
		}

		public override async Task<DateTime?> GetLastWriteTime(string path)
		{
			DateTime? lastRun = null;
			try
			{
				var props = await _blobContainerClient.GetBlobClient(path).GetPropertiesAsync();
				lastRun = props.Value.LastModified.UtcDateTime;
			}
			catch { }

			return lastRun;
		}

		public override async Task<bool> UploadAvatar(int userId, Stream contents, string fileName)
		{
			try
			{
				var name = $"{_config.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(fileName)}";
				using var destination = await _blobContainerClient.GetBlobClient(CombineToRelativePath(AvatarsPath, name)).OpenWriteAsync(overwrite: true);
				await contents.CopyToAsync(destination);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Error uploading avatar.");
				return false;
			}
		}

		public override async Task WriteAllTextToFile(string path, string contents)
		{

			using var source = new MemoryStream();
			using var writer = new StreamWriter(source);
			await writer.WriteAsync(contents);
			await writer.FlushAsync();
			source.Seek(0, SeekOrigin.Begin);

			using var destination = await _blobContainerClient.GetBlobClient(path).OpenWriteAsync(overwrite: true);
			await source.CopyToAsync(destination);
		}

		private string GetFilePath(string name, FileType fileType)
			=> fileType switch
			{
				FileType.Attachment => CombineToRelativePath(AttachmentsPath, name),
				FileType.Avatar => CombineToRelativePath(AvatarsPath, name),
				_ => throw new ArgumentException($"Unknown value '{fileType}'.", nameof(fileType))
			};

		private async Task<bool> DeleteFile(string name, FileType fileType)
		{
			try
			{
				await _blobContainerClient.DeleteBlobIfExistsAsync(GetFilePath(name, fileType), DeleteSnapshotsOption.IncludeSnapshots);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "Error deleting file '{name}'.", name);
				return false;
			}
		}
	}
}
