using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Forum.ForumDb;
using PhpbbInDotnet.Forum.ForumDb.Entities;
using PhpbbInDotnet.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Services
{
    public class StorageService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;
        private readonly ForumDbContext _context;
        private readonly string _root;

        public string AttachmentsPath => Path.Combine(_root,  _config["Storage:Files"]);
        public string AvatarsPath => Path.Combine(_root, _config["Storage:Avatars"]);

        public StorageService(IConfiguration config, Utils utils, IWebHostEnvironment environment, ForumDbContext context)
        {
            _config = config;
            _utils = utils;
            _context = context;
            if (environment.IsProduction() && _config.GetValue<bool>("Storage:IsBetaVersion"))
            {
                _root = @"C:\Inetpub\vhosts\metrouusor.com\forum.metrouusor.com\wwwroot";
            }
            else
            {
                _root = environment.WebRootPath;
            }
        }

        public string GetFileUrl(string name, bool isAvatar)
            => isAvatar ? $"{_config["Storage:Avatars"]}/{name}" : $"{_config["Storage:Files"]}/{name}";

        public string GetFilePath(string name, bool isAvatar)
            => isAvatar ? Path.Combine(AvatarsPath, name) : Path.Combine(AttachmentsPath, name);

        public async Task<(List<PhpbbAttachments> SucceededUploads, List<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
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
                    succeeded.Add((await _context.PhpbbAttachments.AddAsync(new PhpbbAttachments
                    {
                        AttachComment = string.Empty,
                        Extension = Path.GetExtension(file.FileName).Trim('.').ToLowerInvariant(),
                        Filetime = DateTime.UtcNow.ToUnixTimestamp(),
                        Filesize = file.Length,
                        Mimetype = file.ContentType,
                        PhysicalFilename = name,
                        RealFilename = Path.GetFileName(file.FileName),
                        PosterId = userId,
                        IsOrphan = 1,
                        AttachId = 0,
                        PostMsgId = 0,
                        TopicId = 0
                    })).Entity);
                }
                catch (Exception ex)
                {
                    _utils.HandleError(ex, "Error uploading attachments.");
                    failed.Add(file.FileName);
                }
            }
            await _context.SaveChangesAsync();
            return (succeeded, failed);
        }

        public async Task<string> UploadAvatar(int userId, IFormFile file)
        {
            try
            {
                var name = $"{userId}_{DateTime.UtcNow.ToUnixTimestamp()}{Path.GetExtension(file.FileName)}";
                using (var input = file.OpenReadStream())
                using (var fs = File.Open(Path.Combine(AvatarsPath, name), FileMode.Create))
                {
                    await input.CopyToAsync(fs);
                }
                return name;
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, "Error uploading avatar.");
                return null;
            }
        }

        public bool DeleteAvatar(int userId)
        {
            var name = Directory.GetFiles(AvatarsPath, $"{userId}_*.*").FirstOrDefault();
            if (File.Exists(name ?? string.Empty))
            {
                return DeleteFile(name, true);
            }
            return false;
        }

        public bool DeleteFile(string name, bool isAvatar)
        {
            try
            {
                var path = isAvatar ? Path.Combine(AvatarsPath, name) : Path.Combine(AttachmentsPath, name);
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
                    File.Delete(Path.Combine(AttachmentsPath, file));
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

        public IEnumerable<FileInfo> ListAttachments()
        {
            try
            {
                return Directory.GetFiles(AttachmentsPath).Select(f =>
                {
                    FileInfo inf = null;
                    try
                    {
                        inf = new FileInfo(f);
                    }
                    catch { }
                    return inf;
                }).Where(inf => inf != null);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, "Error listing attachments");
                return Enumerable.Empty<FileInfo>();
            }
        }
    }
}
