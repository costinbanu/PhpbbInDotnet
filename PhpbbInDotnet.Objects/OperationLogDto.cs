using System;
using System.Collections.Generic;
using System.Text;

namespace PhpbbInDotnet.Objects
{
    public class OperationLogDto
    {
        public int UserId { get; set; }

        public Enum? Action { get; set; }
    }
}
