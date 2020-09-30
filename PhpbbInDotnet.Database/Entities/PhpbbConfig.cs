namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbConfig
    {
        public string ConfigName { get; set; }
        public string ConfigValue { get; set; }
        public byte IsDynamic { get; set; }
    }
}
