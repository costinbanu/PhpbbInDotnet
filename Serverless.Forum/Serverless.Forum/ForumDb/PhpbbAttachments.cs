using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbAttachments
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttachId { get; set; } = 0;
        public int PostMsgId { get; set; } = 0;
        public int TopicId { get; set; } = 0;
        public byte InMessage { get; set; } = 0;
        public int PosterId { get; set; } = 0;
        public byte IsOrphan { get; set; } = 1;
        public string PhysicalFilename { get; set; } = string.Empty;
        public string RealFilename { get; set; } = string.Empty;
        public int DownloadCount { get; set; } = 0;
        public string AttachComment { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Mimetype { get; set; } = string.Empty;
        public long Filesize { get; set; } = 0;
        public long Filetime { get; set; } = 0;
        public byte Thumbnail { get; set; } = 0;
    }
}
