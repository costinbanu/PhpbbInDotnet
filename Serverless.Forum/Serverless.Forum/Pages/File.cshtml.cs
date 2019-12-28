using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class FileModel : PageModel
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;

        public FileModel(IConfiguration config)
        {
            _config = config;
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
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

                return await Renderfile(file);
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

                return await Renderfile($"avatars/{userId}{Path.GetExtension(file)}");
            }
        }

        private async Task<IActionResult> Renderfile(PhpbbAttachments file)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _config["AwsS3BucketName"],
                    Key = file.PhysicalFilename
                };

                using (var response = await _s3Client.GetObjectAsync(request))
                using (var responseStream = new MemoryStream())
                {
                    await response.ResponseStream.CopyToAsync(responseStream);

                    HttpContext.Response.Headers.Add("content-disposition", $"{(file.Mimetype.IsMimeTypeInline() ? "inline" : "attachment")}; filename={file.RealFilename}");

                    return File(responseStream.GetBuffer(), file.Mimetype);
                }
            }
            catch (AmazonS3Exception s3ex) when (s3ex.Message == "The specified key does not exist.")
            {
                return NotFound($"Fișierul {file.AttachId} nu a fost găsit.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<IActionResult> Renderfile(string fileName)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _config["AwsS3BucketName"],
                    Key = fileName
                };

                var mimeType = new FileExtensionContentTypeProvider().Mappings[Path.GetExtension(fileName)];
                using (var response = await _s3Client.GetObjectAsync(request))
                using (var responseStream = new MemoryStream())
                {
                    await response.ResponseStream.CopyToAsync(responseStream);

                    HttpContext.Response.Headers.Add("content-disposition", $"{(mimeType.IsMimeTypeInline() ? "inline" : "attachment")}; filename={fileName}");

                    return File(responseStream.GetBuffer(), mimeType);
                }
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