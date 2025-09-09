using Newtonsoft.Json;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using System.Collections.Generic;

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
        public int PostId { get; set; }
        public bool DeletedFile { get; }
        public List<string> HighlightWords { get; }

        [JsonIgnore]
        public string FileUrl
        {
            get
            {
                if (DeletedFile)
                {
                    return $"/File?id={Id}&postId={PostId}&handler=deletedFile";
                }
                else
                {
                    var url = $"/File?id={Id}";
                    if (IsPreview)
                    {
                        url += "&preview=true";
                    }
                    if (StringUtility.IsMimeTypeInline(MimeType))
                    {
                        url += $"&postId={PostId}";
                    }
                    return url;
                }
            }
        }

        public AttachmentDto(PhpbbAttachments dbRecord, int forumId, bool isPreview, string language, int postId, bool deletedFile = false, List<string>? highlightWords = null)
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
            PostId = postId;
            DeletedFile = deletedFile;
            HighlightWords = highlightWords ?? [];
        }

        [JsonConstructor]
        public AttachmentDto(int id, string? displayName, string? physicalFileName, int forumId, string? mimeType, int downloadCount, string? comment, long fileSize, string language, bool isPreview, int? postId, bool deletedFile, List<string>? highlightWords)
        {
            Id = id;
            DisplayName = displayName;
            PhysicalFileName = physicalFileName;
            ForumId = forumId;
            MimeType = mimeType;
            DownloadCount = downloadCount;
            Comment = comment;
            FileSize = fileSize;
            Language = language;
            IsPreview = isPreview;
            PostId = postId ?? 0;
            DeletedFile = deletedFile;
            HighlightWords = highlightWords ?? [];
        }
    }
}
