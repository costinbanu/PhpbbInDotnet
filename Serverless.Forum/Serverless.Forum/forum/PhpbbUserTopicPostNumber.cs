using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.forum
{
    public partial class PhpbbUserTopicPostNumber
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public int PostNo { get; set; }
    }
}
