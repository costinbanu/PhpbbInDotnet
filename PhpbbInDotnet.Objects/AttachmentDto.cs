﻿using Newtonsoft.Json;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using System;
using System.Threading.Tasks.Dataflow;

namespace PhpbbInDotnet.Objects
{
    public class AttachmentDto
    {
        public int Id { get; }
        public string? DisplayName { get; }
        public string? PhysicalFileName { get; }
        public int ForumId { get; }
        public string? MimeType { get; }
        public int DownloadCount { get; }
        public string? Comment { get; }
        public long FileSize { get; }
        public string Language { get; } = Constants.DEFAULT_LANGUAGE;
        public bool IsPreview { get; }
        public Guid? CorrelationId { get; set; }
        public bool DeletedFile { get; }

        [JsonIgnore]
        public string FileUrl
        {
            get
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


        public AttachmentDto(PhpbbAttachments dbRecord, int forumId, bool isPreview, string language, Guid? correlationId = null, bool deletedFile = false)
        {
            DisplayName = dbRecord.RealFilename;
            Comment = dbRecord.AttachComment;
            Id = dbRecord.AttachId;
            MimeType = dbRecord.Mimetype;
            DownloadCount = dbRecord.DownloadCount;
            FileSize = dbRecord.Filesize;
            PhysicalFileName = dbRecord.PhysicalFilename;
            ForumId = forumId;
            Language = language;
            IsPreview = isPreview;
            CorrelationId = correlationId;
            DeletedFile = deletedFile;
        }
    }
}
