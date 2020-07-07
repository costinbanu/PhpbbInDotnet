using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbWords
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WordId { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;
    }
}
