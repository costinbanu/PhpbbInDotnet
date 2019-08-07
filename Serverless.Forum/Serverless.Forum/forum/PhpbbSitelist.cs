using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbSitelist
    {
        public int SiteId { get; set; }
        public string SiteIp { get; set; }
        public string SiteHostname { get; set; }
        public byte IpExclude { get; set; }
    }
}
