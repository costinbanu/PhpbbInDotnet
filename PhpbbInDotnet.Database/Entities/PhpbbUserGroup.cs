namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbUserGroup
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public byte GroupLeader { get; set; } = 0;
        public byte UserPending { get; set; } = 0;
    }
}
