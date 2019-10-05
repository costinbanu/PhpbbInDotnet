using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class FileModel : PageModel
    {
        forumContext _dbContext;
        IAmazonS3 _s3Client;
        IConfiguration _config;

        public FileModel(forumContext dbContext, IConfiguration config)
        {
            _dbContext = dbContext;
            _config = config;
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
        }

        public async Task<IActionResult> OnGet(int Id)
        {
            var file = await (from a in _dbContext.PhpbbAttachments
                              where a.AttachId == Id
                              select a).FirstOrDefaultAsync();

            if (file == null)
            {
                return NotFound();
            }

            return await Renderfile(file);
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
    }
}