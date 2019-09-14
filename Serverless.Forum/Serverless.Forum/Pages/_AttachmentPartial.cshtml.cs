using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System.Linq;

namespace Serverless.Forum.Pages
{
    public class _AttachmentPartialModel : PageModel
    {
        public bool IsRenderedInline = false;
        public bool IsDisplayedInline = false;
        public int Id;
        public string FileName;
        public string MimeType;

        //forumContext _dbContext;

        //public _AttachmentPartialModel(forumContext dbContext)
        //{
        //    _dbContext = dbContext;
        //}

        public void OnGet(/*int Id*/)
        {
            //var file = (from a in _dbContext.PhpbbAttachments
            //            where a.AttachId == Id
            //            select a).FirstOrDefault();

            //IsInline = file?.Mimetype?.IsMimeTypeInline() ?? false;
            //FileName = file?.RealFilename;
            //MimeType = file?.Mimetype;
            //this.Id = Id;
        }
    }
}