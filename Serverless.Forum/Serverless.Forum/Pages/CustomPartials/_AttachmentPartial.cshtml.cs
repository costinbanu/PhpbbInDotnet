using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.ForumDb;
using System.Web;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _AttachmentPartialModel : PageModel
    {
        public int Id { get; private set; }
        public string DisplayName { get; private set; }
        public string PhysicalFileName { get; private set; }
        public string MimeType { get; private set; }
        public int DownloadCount { get; private set; }
        public string Comment { get; private set; }
        public long FileSize { get; private set; }
        public string FileUrl { get; private set; }

        public _AttachmentPartialModel(PhpbbAttachments dbAttachmentRecord, bool isPreview = false)
        {
            DisplayName = dbAttachmentRecord.RealFilename;
            Comment = dbAttachmentRecord.AttachComment;
            Id = dbAttachmentRecord.AttachId;
            MimeType = dbAttachmentRecord.Mimetype;
            DownloadCount = dbAttachmentRecord.DownloadCount;
            FileSize = dbAttachmentRecord.Filesize;
            PhysicalFileName = dbAttachmentRecord.PhysicalFilename;
            if (isPreview)
            {
                FileUrl = $"/File?physicalFileName={HttpUtility.UrlEncode(PhysicalFileName)}&realFileName={HttpUtility.UrlEncode(DisplayName)}&mimeType={HttpUtility.UrlEncode(MimeType)}&handler=preview";
            }
            else
            {
                FileUrl = $"/File?id={Id}";
            }
        }
    }
}