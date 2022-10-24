using Newtonsoft.Json;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using System;

namespace PhpbbInDotnet.Objects
{
    public class AttachmentDto
    {
        public int Id { get; set; }
        public string? DisplayName { get; set; }
        public string? PhysicalFileName { get; set; }
        public string? MimeType { get; set; }
        public int DownloadCount { get; set; }
        public string? Comment { get; set; }
        public long FileSize { get; set; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
        public bool IsPreview { get; set; }
        public Guid? CorrelationId { get; set; }
        public bool DeletedFile { get; set; }

        [JsonIgnore]
        public string FileUrl => GetUrl();


        public AttachmentDto(PhpbbAttachments dbRecord, bool isPreview, string language, Guid? correlationId = null, bool deletedFile = false)
        {
            DisplayName = dbRecord.RealFilename;
            Comment = dbRecord.AttachComment;
            Id = dbRecord.AttachId;
            MimeType = dbRecord.Mimetype;
            DownloadCount = dbRecord.DownloadCount;
            FileSize = dbRecord.Filesize;
            PhysicalFileName = dbRecord.PhysicalFilename;
            Language = language;
            IsPreview = isPreview;
            CorrelationId = correlationId;
            DeletedFile = deletedFile;
        }

        public AttachmentDto() { }

        private string GetUrl()
        {
            if (DeletedFile)
            {
                return $"/File?id={Id}&correlationId={CorrelationId}&handler=deletedFile";
            }
            else
            {
                var url = $"/File?id={Id}&preview={IsPreview}";
                if (CorrelationId.HasValue && StringUtility.IsMimeTypeInline(MimeType))
                {
                    url += $"&correlationId={CorrelationId.Value}";
                }
                return url;
            }
        }
    }
}
