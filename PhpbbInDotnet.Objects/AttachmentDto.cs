using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Database.Entities;
using System;
using System.Web;

namespace PhpbbInDotnet.Objects
{
    public class AttachmentDto
    {
        public int Id { get; private set; }
        public string DisplayName { get; private set; }
        public string PhysicalFileName { get; private set; }
        public string MimeType { get; private set; }
        public int DownloadCount { get; private set; }
        public string Comment { get; private set; }
        public long FileSize { get; private set; }
        public string FileUrl { get; private set; }
        public string Language { get; private set; }

        public AttachmentDto(PhpbbAttachments dbRecord, bool isPreview, string language, Guid? correlationId = null)
        {
            DisplayName = dbRecord.RealFilename;
            Comment = dbRecord.AttachComment;
            Id = dbRecord.AttachId;
            MimeType = dbRecord.Mimetype;
            DownloadCount = dbRecord.DownloadCount;
            FileSize = dbRecord.Filesize;
            PhysicalFileName = dbRecord.PhysicalFilename;
            Language = language;
            FileUrl = $"/File?id={Id}&preview={isPreview}";
            
            if (correlationId.HasValue)
            {
                FileUrl += $"&correlationId={correlationId.Value}";
            }
        }
    }
}
