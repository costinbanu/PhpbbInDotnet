using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbTopics
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TopicId { get; set; } = 0;
        public int ForumId { get; set; } = 0;
        public int IconId { get; set; } = 0;
        public byte TopicAttachment { get; set; } = 0;
        public byte TopicApproved { get; set; } = 1;
        public byte TopicReported { get; set; } = 0;
        public string TopicTitle { get; set; } = string.Empty;
        public int TopicPoster { get; set; } = 0;
        public long TopicTime { get; set; } = 0;
        public long TopicTimeLimit { get; set; } = 0;
        public int TopicViews { get; set; } = 0;
        public int TopicReplies { get; set; } = 0;
        public int TopicRepliesReal { get; set; } = 0;
        public byte TopicStatus { get; set; } = 0;
        public byte TopicType { get; set; } = 0;
        public int TopicFirstPostId { get; set; } = 0;
        public string TopicFirstPosterName { get; set; } = string.Empty;
        public string TopicFirstPosterColour { get; set; } = string.Empty;
        public int TopicLastPostId { get; set; } = 0;
        public int TopicLastPosterId { get; set; } = 0;
        public string TopicLastPosterName { get; set; } = string.Empty;
        public string TopicLastPosterColour { get; set; } = string.Empty;
        public string TopicLastPostSubject { get; set; } = string.Empty;
        public long TopicLastPostTime { get; set; } = 0;
        public long TopicLastViewTime { get; set; } = 0;
        public int TopicMovedId { get; set; } = 0;
        public byte TopicBumped { get; set; } = 0;
        public int TopicBumper { get; set; } = 0;
        public string PollTitle { get; set; }
        public long PollStart { get; set; } = 0;
        public int PollLength { get; set; } = 0;
        public byte PollMaxOptions { get; set; } = 1;
        public long PollLastVote { get; set; } = 0;
        public byte PollVoteChange { get; set; } = 0;
    }
}
