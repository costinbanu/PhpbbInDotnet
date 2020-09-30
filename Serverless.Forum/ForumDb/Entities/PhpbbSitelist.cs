using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbSitelist
    {
        public int SiteId { get; set; }
        public string SiteIp { get; set; }
        public string SiteHostname { get; set; }
        public byte IpExclude { get; set; }
    }
}
