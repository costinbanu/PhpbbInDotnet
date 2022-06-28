using PhpbbInDotnet.Domain;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class TopicGroup
    {
        public TopicType? TopicType { get; set; } = null;

        public IEnumerable<TopicDto>? Topics { get; set; }
    }
}
