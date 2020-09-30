using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.DTOs
{
    public class ForumPermissions
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public AclEntityType Type { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string RoleDescription { get; set; }
        public bool HasRole { get; set; }
    }
}
