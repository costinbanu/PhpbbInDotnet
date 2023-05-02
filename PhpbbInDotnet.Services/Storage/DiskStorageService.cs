using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
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
        private readonly ILogger _logger;

        public DiskStorageService(IConfiguration config, IWebHostEnvironment environment, ISqlExecuter sqlExecuter, ILogger logger)
            : base(config, environment, sqlExecuter)
        {
            _config = config;
            _logger = logger;
        }

        public override async Task<(List<PhpbbAttachments> SucceededUploads, List<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
        {
            var succeeded = new List<PhpbbAttachments>();
            var failed = new List<string>();

            foreach (var file in attachedFiles)
            {
                try
                {
                    var name = $"{userId}_{Guid.NewGuid():n}";
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

        public override string? DuplicateFile(PhpbbAttachments attachment, int userId)
        {
            try
            {
                var name = $"{userId}_{Guid.NewGuid():n}";
                File.Copy(Path.Combine(AttachmentsPath, attachment.PhysicalFilename), Path.Combine(AttachmentsPath, name));
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

        public override bool DeleteAvatar(int userId, string extension)
        {
            var name = $"{_config.GetValue<string>("AvatarSalt")}_{userId}.{extension.TrimStart('.')}";
            if (File.Exists(Path.Combine(AvatarsPath, name)))
            {
                return DeleteFile(name, true);
            }
            return false;
        }

        public override bool DeleteFile(string? name, bool isAvatar)
        {
            try
            {
                var path = isAvatar ? Path.Combine(AvatarsPath, name!) : Path.Combine(AttachmentsPath, name!);
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting file '{name}'.", name);
                return false;
            }
        }

        public override (IEnumerable<string> Succeeded, IEnumerable<string> Failed) BulkDeleteAttachments(IEnumerable<string> files)
        {
            var succeeded = new List<string>();
            var failed = new List<string>();
            foreach (var file in files)
            {
                try
                {
                    File.Delete(Path.Combine(AttachmentsPath, file));
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

        public override async Task<byte[]> GetAttachmentContents(string name)
        {
            try
            {
                return await File.ReadAllBytesAsync(Path.Combine(AttachmentsPath, name));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting attachment contents for '{name}'", name);
                return Array.Empty<byte>();
            }
        }

        public override void WriteAllTextToFile(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }
    }
}
