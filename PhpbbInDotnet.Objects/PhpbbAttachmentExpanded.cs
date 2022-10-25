using PhpbbInDotnet.Database.Entities;

namespace PhpbbInDotnet.Objects
{
    public class PhpbbAttachmentExpanded : PhpbbAttachments
    {
        public PhpbbAttachmentExpanded(PhpbbAttachments @base, int forumId)
        {
            AttachId = @base.AttachId;
            PostMsgId = @base.PostMsgId;
            TopicId = @base.TopicId;
            InMessage = @base.InMessage;
            PosterId = @base.PosterId;
            IsOrphan = @base.IsOrphan;
            PhysicalFilename = @base.PhysicalFilename;
            RealFilename = @base.RealFilename;
            DownloadCount = @base.DownloadCount;
            AttachComment = @base.AttachComment;
            Extension = @base.Extension;
            Mimetype = @base.Mimetype;
            Filesize = @base.Filesize;
            Filetime = @base.Filetime;
            Thumbnail = @base.Thumbnail;
            ForumId = forumId;
        }

        public PhpbbAttachmentExpanded() { }

        public int ForumId { get; set; }
    }
}
