using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class StorageService
    {
        private readonly IConfiguration _config;
        private readonly Storage _storageOptions;
        private readonly CommonUtils _utils;
        private readonly ForumDbContext _context;
        private readonly string _attachmentsPath;
        private readonly string _avatarsPath;

        public StorageService(IConfiguration config, CommonUtils utils, IWebHostEnvironment environment, ForumDbContext context)
        {
            _config = config;
            _utils = utils;
            _context = context;

            _storageOptions = _config.GetObject<Storage>();
            _attachmentsPath = Path.Combine(environment.WebRootPath, _storageOptions.Files);
            _avatarsPath = Path.Combine(environment.WebRootPath, _storageOptions.Avatars);
        }

        public string GetFileUrl(string name, bool isAvatar)
            => isAvatar ? $"{_storageOptions.Avatars}/{name}" : $"{_storageOptions.Files}/{name}";

        public string GetFilePath(string name, bool isAvatar)
            => isAvatar ? Path.Combine(_avatarsPath, name) : Path.Combine(_attachmentsPath, name);

        public async Task<(List<PhpbbAttachments> SucceededUploads, List<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
        {
            var succeeded = new List<PhpbbAttachments>();
            var failed = new List<string>();

            var conn = _context.Database.GetDbConnection();

            foreach (var file in attachedFiles)
            {
                try
                {
                    var name = $"{userId}_{Guid.NewGuid():n}";
                    using (var input = file.OpenReadStream())
                    using (var fs = File.Open(Path.Combine(_attachmentsPath, name), FileMode.Create))
                    {
                        await input.CopyToAsync(fs);
                    }

                    succeeded.Add(
                        await conn.QueryFirstOrDefaultAsync<PhpbbAttachments>(
                            "INSERT INTO phpbb_attachments (attach_comment, extension, filetime, filesize, mimetype, physical_filename, real_filename, poster_id) " +
                            "VALUES ('', @Extension, @Filetime, @Filesize, @Mimetype, @PhysicalFilename, @RealFilename, @PosterId); " +
                            "SELECT * FROM phpbb_attachments WHERE attach_id = LAST_INSERT_ID()",
                            new
                            {
                                AttachComment = string.Empty,
                                Extension = Path.GetExtension(file.FileName).Trim('.').ToLowerInvariant(),
                                Filetime = DateTime.UtcNow.ToUnixTimestamp(),
                                Filesize = file.Length,
                                Mimetype = file.ContentType,
                                PhysicalFilename = name,
                                RealFilename = Path.GetFileName(file.FileName),
                                PosterId = userId,
                            }
                        )
                    );
                }
                catch (Exception ex)
                {
                    _utils.HandleError(ex, $"Error uploading attachment by user {userId}.");
                    failed.Add(file.FileName);
                }
            }

            return (succeeded, failed);
        }

        public string DuplicateFile(PhpbbAttachments attachment, int userId)
        {
            try
            {
                var name = $"{userId}_{Guid.NewGuid():n}";
                File.Copy(Path.Combine(_attachmentsPath, attachment.PhysicalFilename), Path.Combine(_attachmentsPath, name));
                return name;
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return null;
            }
        }

        public async Task<bool> UploadAvatar(int userId, Stream contents, string fileName)
        {
            try
            {
                var name = $"{_config.GetValue<string>("AvatarSalt")}_{userId}{Path.GetExtension(fileName)}";
                using var fs = File.Open(Path.Combine(_avatarsPath, name), FileMode.Create);
                await contents.CopyToAsync(fs);
                await fs.FlushAsync();
                return true;
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, "Error uploading avatar.");
                return false;
            }
        }

        public bool DeleteAvatar(int userId, string extension)
        {
            var name = $"{_config.GetValue<string>("AvatarSalt")}_{userId}.{extension.TrimStart('.')}";
            if (File.Exists(Path.Combine(_avatarsPath, name)))
            {
                return DeleteFile(name, true);
            }
            return false;
        }

        public bool DeleteFile(string name, bool isAvatar)
        {
            try
            {
                var path = isAvatar ? Path.Combine(_avatarsPath, name) : Path.Combine(_attachmentsPath, name);
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, $"Error deleting file '{name}'.");
                return false;
            }
        }

        public (IEnumerable<string> Succeeded, IEnumerable<string> Failed) BulkDeleteAttachments(IEnumerable<string> files)
        {
            var succeeded = new List<string>();
            var failed = new List<string>();
            foreach (var file in files)
            {
                try
                {
                    File.Delete(Path.Combine(_attachmentsPath, file));
                    succeeded.Add(file);
                }
                catch (Exception ex)
                {
                    _utils.HandleError(ex, $"Error deleting file '{file}'.");
                    failed.Add(file);
                }
            }
            return (succeeded, failed);
        }
    }
}
