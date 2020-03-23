using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class StorageService
    {
        private readonly IConfiguration _config;
        private readonly IHostingEnvironment _hostingEnvironment;

        public StorageService(IConfiguration config, IHostingEnvironment hostingEnvironment)
        {
            _config = config;
            _hostingEnvironment = hostingEnvironment;
        }

        public async Task<Stream> ReadFile(string name)
        {
            //var request = new GetObjectRequest
            //{
            //    BucketName = _config["AwsS3BucketName"],
            //    Key = $"{(!_hostingEnvironment.IsProduction() ? "testing/" : string.Empty)}{name}"
            //};

            ////using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            ////using (var response = await s3client.GetObjectAsync(request))
            ////using (var responseStream = new MemoryStream())
            ////{
            ////    ;
            ////    await response.ResponseStream.CopyToAsync(responseStream);
            ////    return responseStream;
            ////}
            //using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            //using (var response = await s3client.GetObjectAsync(request))
            ////using (var responseStream = new MemoryStream())
            //{
            //    return response.ResponseStream;
            //}

            async Task<Stream> Impl(string fileName)
            {
                var request = new GetObjectRequest
                {
                    BucketName = _config["AwsS3BucketName"],
                    Key = fileName
                };
                using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
                using (var response = await s3client.GetObjectAsync(request))
                {
                    var result = new MemoryStream();
                    await response.ResponseStream.CopyToAsync(result);
                    result.Seek(0, SeekOrigin.Begin);
                    return result;
                }
            }


            if (_hostingEnvironment.IsProduction())
            {
                return await Impl(name);
            }
            else
            {
                try
                {
                    return await Impl(name);
                }
                catch (AmazonS3Exception s3ex) when (s3ex.Message == "The specified key does not exist.")
                {
                    return await Impl($"testing/{name}");
                }
            }
        }

        public async Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
        {
            var succeeded = new List<PhpbbAttachments>();
            var failed = new List<string>();
            using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            {
                 foreach(var file in attachedFiles)
                 {
                     var name = $"{userId}_{Guid.NewGuid():n}";

                     if (!await UploadFileImpl(name, file.ContentType, file.OpenReadStream(), s3client))
                     {
                         failed.Add(file.FileName);
                     }
                     else
                     {
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
                 }
                return (succeeded, failed);
            }
        }

        public async Task<bool> UploadFile(string name, string contentType, Stream content)
        {
            using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            {
                return await UploadFileImpl(name, contentType, content, s3client);
            }
        }

        public async Task<bool> DeleteFile(string name)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Key = $"{(!_hostingEnvironment.IsProduction() ? "testing/" : string.Empty)}{name}",
            };
            using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            {
                var response = await s3client.DeleteObjectAsync(request);
                return response.HttpStatusCode == HttpStatusCode.NoContent;
            }
        }

        private async Task<bool> UploadFileImpl(string name, string contentType, Stream content, AmazonS3Client s3client)
        {
            var request = new PutObjectRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Key = $"{(!_hostingEnvironment.IsProduction() ? "testing/" : string.Empty)}{name}",
                ContentType = contentType,
                InputStream = content
            };

            var response = await s3client.PutObjectAsync(request);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
    }
}
