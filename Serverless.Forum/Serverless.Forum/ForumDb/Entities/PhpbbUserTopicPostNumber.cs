using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbUserTopicPostNumber
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; } = 0;
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public int PostNo { get; set; }
    }
}
