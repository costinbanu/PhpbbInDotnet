using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbExtensionGroups
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public byte CatId { get; set; }
        public byte AllowGroup { get; set; }
        public byte DownloadMode { get; set; }
        public string UploadIcon { get; set; }
        public int MaxFilesize { get; set; }
        public string AllowedForums { get; set; }
        public byte AllowInPm { get; set; }
    }
}
