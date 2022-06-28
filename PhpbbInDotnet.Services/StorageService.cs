using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class StorageService : IStorageService
    {
        private readonly IConfiguration _config;
        private readonly Storage _storageOptions;
        private readonly ICommonUtils _utils;
        private readonly IForumDbContext _context;
        private readonly string _attachmentsPath;
        private readonly string _avatarsPath;
        private readonly string _emojiPath;

        public StorageService(IConfiguration config, ICommonUtils utils, IWebHostEnvironment environment, IForumDbContext context)
        {
            _config = config;
            _utils = utils;
            _context = context;

            _storageOptions = _config.GetObject<Storage>();
            _attachmentsPath = Path.Combine(environment.WebRootPath, _storageOptions.Files!);
            _avatarsPath = Path.Combine(environment.WebRootPath, _storageOptions.Avatars!);
            _emojiPath = Path.Combine(environment.WebRootPath, _storageOptions.Emojis!);
        }

        public string? GetFileUrl(string name, FileType fileType)
            => fileType switch
            {
                FileType.Attachment => $"./{_storageOptions.Files!.Trim('/')}/{name.TrimStart('/')}",
                FileType.Avatar => $"./{_storageOptions.Avatars!.Trim('/')}/{name.TrimStart('/')}",
                FileType.Emoji => $"./{_storageOptions.Emojis!.Trim('/')}/{name.TrimStart('/')}",
                _ => null
            };

        public string? GetFilePath(string name, FileType fileType)
            => fileType switch
            {
                FileType.Attachment => Path.Combine(_attachmentsPath, name),
                FileType.Avatar => Path.Combine(_avatarsPath, name),
                FileType.Emoji => Path.Combine(_emojiPath, name),
                _ => null
            };

        public async Task<(List<PhpbbAttachments> SucceededUploads, List<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
        {
            var succeeded = new List<PhpbbAttachments>();
            var failed = new List<string>();

            var sqlExecuter = _context.GetSqlExecuter();

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
                        await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbAttachments>(
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

        public string? DuplicateFile(PhpbbAttachments attachment, int userId)
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

        public bool DeleteFile(string? name, bool isAvatar)
        {
            try
            {
                var path = isAvatar ? Path.Combine(_avatarsPath, name!) : Path.Combine(_attachmentsPath, name!);
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
                _utils.HandleError(ex, $"Error uploading emoji '{name}'");
                return false;
            }
        }

        public async Task<byte[]> GetAttachmentContents(string name)
        {
            try
            {
                return await File.ReadAllBytesAsync(Path.Combine(_attachmentsPath, name));
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, $"Error getting attachment contents for '{name}'");
                return Array.Empty<byte>();
            }
        }
    }
}
