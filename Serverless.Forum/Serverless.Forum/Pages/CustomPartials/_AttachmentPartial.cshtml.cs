using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.ForumDb.Entities;
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

        public _AttachmentPartialModel(string realFilename, string attachComment, int attachId, string mimetype, int downloadCount, long filesize, string physicalFilename, bool isPreview = false)
        {
            DisplayName = realFilename;
            Comment = attachComment;
            Id = attachId;
            MimeType = mimetype;
            DownloadCount = downloadCount;
            FileSize = filesize;
            PhysicalFileName = physicalFilename;
            if (isPreview)
            {
                FileUrl = $"/File?physicalFileName={HttpUtility.UrlEncode(PhysicalFileName)}&realFileName={HttpUtility.UrlEncode(DisplayName)}&mimeType={HttpUtility.UrlEncode(MimeType)}&handler=preview";
            }
            else
            {
                FileUrl = $"/File?id={Id}";
            }
        }

        public _AttachmentPartialModel(PhpbbAttachments dbRecord, bool isPreview = false)
            : this(dbRecord.RealFilename, dbRecord.AttachComment, dbRecord.AttachId, dbRecord.Mimetype, dbRecord.DownloadCount, dbRecord.Filesize, dbRecord.PhysicalFilename, isPreview)
        { }
    }
}