using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbRanks
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RankId { get; set; } = 0;
        public string RankTitle { get; set; } = string.Empty;
        public int RankMin { get; set; } = 0;
        public byte RankSpecial { get; set; } = 0;
        public string RankImage { get; set; } = string.Empty;
    }
}
