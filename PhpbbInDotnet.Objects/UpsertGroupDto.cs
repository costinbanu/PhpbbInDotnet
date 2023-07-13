namespace PhpbbInDotnet.Objects
{
    public class UpsertGroupDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? Desc { get; set; }

        public int Rank { get; set; }

        public string? Color { get; set; }

        public string? DbColor => Color?.TrimStart('#');

        public long UploadLimit { get; set; }

        public int EditTime { get; set; }

        public bool? Delete { get; set; }

        public int Role { get; set; }
    }
}
