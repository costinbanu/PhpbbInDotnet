using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbForums
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ForumId { get; set; } = 0;
        public int ParentId { get; set; } = 0;
        public int LeftId { get; set; } = 0;
        public int RightId { get; set; } = 0;
        public string ForumParents { get; set; } = string.Empty;
        public string ForumName { get; set; } = string.Empty;
        public string ForumDesc { get; set; } = string.Empty;
        public string ForumDescBitfield { get; set; } = string.Empty;
        public int ForumDescOptions { get; set; } = 7;
        public string ForumDescUid { get; set; } = string.Empty;
        public string ForumLink { get; set; } = string.Empty;
        public string ForumPassword { get; set; } = string.Empty;
        public int ForumStyle { get; set; } = 0;
        public string ForumImage { get; set; } = string.Empty;
        public string ForumRules { get; set; } = string.Empty;
        public string ForumRulesLink { get; set; } = string.Empty;
        public string ForumRulesBitfield { get; set; } = string.Empty;
        public int ForumRulesOptions { get; set; } = 7;
        public string ForumRulesUid { get; set; } = string.Empty;
        public byte ForumTopicsPerPage { get; set; } = 0;
        [Column(TypeName = "tinyint(2)")]
        public ForumType ForumType { get; set; } = ForumType.Category;
        public byte ForumStatus { get; set; } = 0;
        public int ForumPosts { get; set; } = 0;
        public int ForumTopics { get; set; } = 0;
        public int ForumTopicsReal { get; set; } = 0;
        public int ForumLastPostId { get; set; } = 0;
        public int ForumLastPosterId { get; set; } = 0;
        public string ForumLastPostSubject { get; set; } = string.Empty;
        public long ForumLastPostTime { get; set; } = 0;
        public string ForumLastPosterName { get; set; } = string.Empty;
        public string ForumLastPosterColour { get; set; } = string.Empty;
        public byte ForumFlags { get; set; } = 32;
        public int ForumOptions { get; set; } = 0;
        public byte DisplaySubforumList { get; set; } = 1;
        public byte DisplayOnIndex { get; set; } = 1;
        public byte EnableIndexing { get; set; } = 1;
        public byte EnableIcons { get; set; } = 1;
        public byte EnablePrune { get; set; } = 0;
        public int PruneNext { get; set; } = 0;
        public int PruneDays { get; set; } = 0;
        public int PruneViewed { get; set; } = 0;
        public int PruneFreq { get; set; } = 0;
        public long ForumEditTime { get; set; } = 0;

        public override bool Equals(object obj)
            => obj is PhpbbForums f && ForumId == f.ForumId;

        public override int GetHashCode()
            => HashCode.Combine(ForumId);
    }
}
