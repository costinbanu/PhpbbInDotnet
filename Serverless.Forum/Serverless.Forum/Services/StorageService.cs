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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class StorageService
    {
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _hostingEnvironment;

        public string FolderPrefix => _config["AWS:S3FolderPrefix"];
        public string AvatarsFolder => _config["AWS:S3AvatarsFolder"];

        public StorageService(IConfiguration config, IHostEnvironment hostingEnvironment)
        {
            _config = config;
            _hostingEnvironment = hostingEnvironment;
        }

        public async Task<bool> FileExists(string fileName)
        {
            using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
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

        public async Task<string> GetFileUrl(string name, string contentDisposition, string contentType)
        {
            string Impl(string fileName)
            {
                using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
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

            if (_hostingEnvironment.IsProduction())
            {
                return Impl(name);
            }
            else if (await FileExists(name))
            {
                return Impl(name);
            }
            else if (await FileExists($"{FolderPrefix}{name}"))
            {
                return Impl($"{FolderPrefix}{name}");
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
            using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
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

        public async Task<bool> UploadFile(string name, string contentType, Stream content)
        {
            using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
            return await UploadFileImpl(name, contentType, content, s3client);
        }

        public async Task<bool> DeleteFile(string name)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Key = $"{FolderPrefix}{name}"
            };
            using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
            var response = await s3client.DeleteObjectAsync(request);
            return response.HttpStatusCode == HttpStatusCode.NoContent;
        }

        public async Task<bool> BulkDeleteFiles(IEnumerable<string> files)
        {
            using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
            var request = new DeleteObjectsRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Objects = (
                    from f in files
                    where string.IsNullOrWhiteSpace(FolderPrefix) || f.StartsWith(FolderPrefix)
                    select new KeyVersion { Key = f }
                ).ToList()
            };
            var response = await s3client.DeleteObjectsAsync(request);
            return response.DeletedObjects.Count == files.Count();
        }

        public async IAsyncEnumerable<S3Object> ListItems(bool onlyCurrentEnvironment = false)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _config["AwsS3BucketName"],
                Prefix = onlyCurrentEnvironment ? FolderPrefix : null
            };
            using var s3client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
            ListObjectsV2Response response;

            do
            {
                response = await s3client.ListObjectsV2Async(request);
                request.ContinuationToken = response.NextContinuationToken;
                foreach (var obj in response.S3Objects)
                {
                    yield return obj;
                }
            } while (response.IsTruncated);
        }

        private async Task<bool> UploadFileImpl(string name, string contentType, Stream content, AmazonS3Client s3client)
        {
            var request = new PutObjectRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Key = $"{FolderPrefix}{name}",
                ContentType = contentType,
                InputStream = content
            };

            var response = await s3client.PutObjectAsync(request);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
    }
}
