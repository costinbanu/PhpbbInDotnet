using Newtonsoft.Json;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class BreadCrumbJSLD
    {
        [JsonProperty("@context")]
        public string Context => "https://schema.org";

        [JsonProperty("@type")]
        public string Type => "BreadcrumbList";

        [JsonProperty("itemListElement")]
        public List<ListItemJSLD> ItemListElement { get; set; } = new();
    }

    public class ListItemJSLD
    {
        [JsonProperty("@type")]
        public string Type => "ListItem";

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("item")]
        public string Item { get; set; } = string.Empty;
    }
}
