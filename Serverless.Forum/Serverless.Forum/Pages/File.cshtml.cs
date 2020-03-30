using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class FileModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly StorageService _storageService;

        public FileModel(IConfiguration config, StorageService storageService)
        {
            _config = config;
            _storageService = storageService;
        }

        public async Task<IActionResult> OnGet(int Id)
        {
            using (var context = new ForumDbContext(_config))
            {
                var file = await (from a in context.PhpbbAttachments
                                  where a.AttachId == Id
                                  select a).FirstOrDefaultAsync();

                if (file == null)
                {
                    return NotFound();
                }

                return Renderfile(file.PhysicalFilename, file.RealFilename, file.Mimetype);
            }
        }

        public async Task<IActionResult> OnGetAvatar(int userId)
        {
            using (var context = new ForumDbContext(_config))
            {
                var file = await (from u in context.PhpbbUsers
                                  where u.UserId == userId
                                  select u.UserAvatar).FirstOrDefaultAsync();

                if (file == null)
                {
                    return NotFound();
                }

                return Renderfile($"avatars/{userId}{Path.GetExtension(file)}", file);
            }
        }

        private IActionResult Renderfile(string fileName, string displayName, string mimeType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mimeType))
                {
                    mimeType = new FileExtensionContentTypeProvider().Mappings[Path.GetExtension(fileName)];
                }
                do this https://stackoverflow.com/questions/93551/how-to-encode-the-filename-parameter-of-content-disposition-header-in-http
                return Redirect(_storageService.ReadFile(fileName, $"{(mimeType.IsMimeTypeInline() ? "inline" : "attachment")}; filename={HttpUtility.UrlEncode(displayName)}", mimeType));
            }
            catch (AmazonS3Exception s3ex) when (s3ex.Message == "The specified key does not exist.")
            {
                return NotFound($"Fișierul {fileName} nu a fost găsit.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}