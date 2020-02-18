using Serverless.Forum.Utilities;

namespace Serverless.Forum.Contracts
{
    public class ForumPermissions
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public AclItemType Type { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string RoleDescription { get; set; }
        public bool HasRole { get; set; }
    }
}
