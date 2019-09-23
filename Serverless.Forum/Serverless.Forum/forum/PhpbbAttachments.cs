using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbAttachments
    {
        public int AttachId { get; set; }
        public int PostMsgId { get; set; }
        public int TopicId { get; set; }
        public byte InMessage { get; set; }
        public int PosterId { get; set; }
        public byte IsOrphan { get; set; }
        public string PhysicalFilename { get; set; }
        public string RealFilename { get; set; }
        public int DownloadCount { get; set; }
        public string AttachComment { get; set; }
        public string Extension { get; set; }
        public string Mimetype { get; set; }
        public long Filesize { get; set; }
        public long Filetime { get; set; }
        public byte Thumbnail { get; set; }
    }
}
