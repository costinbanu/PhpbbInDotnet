using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbDrafts
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DraftId { get; set; } = 0;
        public int UserId { get; set; } = 0;
        public int TopicId { get; set; } = 0;
        public int ForumId { get; set; } = 0;
        public long SaveTime { get; set; } = 0;
        [StringLength(255)]
        public string DraftSubject { get; set; } = string.Empty;
        public string DraftMessage { get; set; } = string.Empty;
    }
}
