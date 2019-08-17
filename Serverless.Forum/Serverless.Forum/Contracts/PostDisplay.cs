using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Contracts
{
    public class PostDisplay
    {
        public int? Id { get; set; } = null;

        public string PostText { get; set; } = null;

        public string AuthorName { get; set; } = null;

        public int? AuthorId { get; set; } = null;

        public DateTime? PostCreationTime { get; set; } = null;

        public DateTime? PostModifiedTime { get; set; } = null;
    }
}
