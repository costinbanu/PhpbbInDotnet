using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
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

        public async Task<bool> FileExists(string fileName)
        {
            using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            {
                try
                {
                    await s3client.GetObjectMetadataAsync(
                        new GetObjectMetadataRequest()
                        {
                            BucketName = _config["AwsS3BucketName"],
                            Key = fileName
                        }
                    );
                    return true;
                }
                catch 
                {
                    return false;
                }
            }
        }

        public async Task<string> GetFileUrl(string name, string contentDisposition, string contentType)
        {
            string Impl(string fileName)
            {
                using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
                {
                    var request = new GetPreSignedUrlRequest
                    {
                        BucketName = _config["AwsS3BucketName"],
                        Key = fileName,
                        Expires = DateTime.Now.AddMinutes(2),
                        ResponseHeaderOverrides = new ResponseHeaderOverrides
                        {
                            ContentDisposition = contentDisposition,
                            ContentType = contentType
                        }
                    };
                    return s3client.GetPreSignedURL(request);
                }
            }

            if (_hostingEnvironment.IsProduction())
            {
                return Impl(name);
            }
            else if (await FileExists(name))
            {
                return Impl(name);
            }
            else if (await FileExists($"testing/{name}"))
            {
                return Impl($"testing/{name}");
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId)
        {
            var succeeded = new List<PhpbbAttachments>();
            var failed = new List<string>();
            using (var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1))
            {
                foreach (var file in attachedFiles)
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
