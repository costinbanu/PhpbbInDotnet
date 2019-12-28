using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbForums
    {
        public int ForumId { get; set; }
        public int ParentId { get; set; }
        public int LeftId { get; set; }
        public int RightId { get; set; }
        public string ForumParents { get; set; }
        public string ForumName { get; set; }
        public string ForumDesc { get; set; }
        public string ForumDescBitfield { get; set; }
        public int ForumDescOptions { get; set; }
        public string ForumDescUid { get; set; }
        public string ForumLink { get; set; }
        public string ForumPassword { get; set; }
        public int ForumStyle { get; set; }
        public string ForumImage { get; set; }
        public string ForumRules { get; set; }
        public string ForumRulesLink { get; set; }
        public string ForumRulesBitfield { get; set; }
        public int ForumRulesOptions { get; set; }
        public string ForumRulesUid { get; set; }
        public byte ForumTopicsPerPage { get; set; }
        public byte ForumType { get; set; }
        public byte ForumStatus { get; set; }
        public int ForumPosts { get; set; }
        public int ForumTopics { get; set; }
        public int ForumTopicsReal { get; set; }
        public int ForumLastPostId { get; set; }
        public int ForumLastPosterId { get; set; }
        public string ForumLastPostSubject { get; set; }
        public long ForumLastPostTime { get; set; }
        public string ForumLastPosterName { get; set; }
        public string ForumLastPosterColour { get; set; }
        public byte ForumFlags { get; set; }
        public int ForumOptions { get; set; }
        public byte DisplaySubforumList { get; set; }
        public byte DisplayOnIndex { get; set; }
        public byte EnableIndexing { get; set; }
        public byte EnableIcons { get; set; }
        public byte EnablePrune { get; set; }
        public int PruneNext { get; set; }
        public int PruneDays { get; set; }
        public int PruneViewed { get; set; }
        public int PruneFreq { get; set; }
        public long ForumEditTime { get; set; }
    }
}
