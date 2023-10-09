using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Objects.Configuration
{
    public class BotConfig
    {
        public DateTime? UnlimitedAccessStartTime { get; set; }
        public DateTime? UnlimitedAccessEndTime { get; set; }
        public int InstanceCountLimit { get; set; }
    }
}
