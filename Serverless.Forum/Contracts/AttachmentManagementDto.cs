using Serverless.Forum.ForumDb.Entities;

namespace Serverless.Forum.Contracts
{
    public class AttachmentManagementDto : PhpbbAttachments
    {
        public string Username { get; set; }
    }
}
