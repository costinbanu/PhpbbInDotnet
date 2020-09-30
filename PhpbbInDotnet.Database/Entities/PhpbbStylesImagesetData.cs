namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbStylesImagesetData
    {
        public int ImageId { get; set; }
        public string ImageName { get; set; }
        public string ImageFilename { get; set; }
        public string ImageLang { get; set; }
        public short ImageHeight { get; set; }
        public short ImageWidth { get; set; }
        public int ImagesetId { get; set; }
    }
}
