namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbAclRoles
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string RoleDescription { get; set; } = string.Empty;
        public string RoleType { get; set; } = string.Empty;
        public short RoleOrder { get; set; }
    }
}
