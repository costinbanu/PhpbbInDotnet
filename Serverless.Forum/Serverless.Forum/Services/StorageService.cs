﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class StorageService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;
        private readonly IWebHostEnvironment _environment;

        public string AttachmentsPath => Path.Combine(_environment.WebRootPath,  _config["Storage:Files"]);
        public string AvatarsPath => Path.Combine(_environment.WebRootPath, _config["Storage:Avatars"]);

        public StorageService(IConfiguration config, Utils utils, IWebHostEnvironment environment)
        {
            _config = config;
            _utils = utils;
            _environment = environment;
        }

        public string GetFileUrl(string name, bool isAvatar)
            => isAvatar ? $"{_config["Storage:Avatars"]}/{name}" : $"{_config["Storage:Files"]}/{name}";

        public string GetFilePath(string name, bool isAvatar)
            => isAvatar ? Path.Combine(AvatarsPath, name) : Path.Combine(AttachmentsPath, name);

        public async Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
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
                    succeeded.Add(new PhpbbAttachments
                    {
                        AttachComment = null,
                        Extension = Path.GetExtension(file.FileName),
                        Filetime = DateTime.UtcNow.ToUnixTimestamp(),
                        Filesize = file.Length,
                        Mimetype = file.ContentType,
                        PhysicalFilename = name,
                        RealFilename = Path.GetFileName(file.FileName),
                        PosterId = userId
                    });
                }
                catch (Exception ex)
                {
                    _utils.HandleError(ex, "Error uploading attachments.");
                    failed.Add(file.FileName);
                }
            }
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
