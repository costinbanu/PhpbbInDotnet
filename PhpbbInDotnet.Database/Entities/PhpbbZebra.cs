namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbZebra
    {
        public int UserId { get; set; } = 0;
        public int ZebraId { get; set; } = 0;
        public byte Friend { get; set; } = 0;
        public byte Foe { get; set; } = 0;
    }
}
