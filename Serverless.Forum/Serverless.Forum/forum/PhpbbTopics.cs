﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbTopics
    {
        public int TopicId { get; set; }
        public int ForumId { get; set; }
        public int IconId { get; set; }
        public byte TopicAttachment { get; set; }
        public byte TopicApproved { get; set; }
        public byte TopicReported { get; set; }
        public string TopicTitle { get; set; }
        public int TopicPoster { get; set; }
        public long TopicTime { get; set; }
        public long TopicTimeLimit { get; set; }
        public int TopicViews { get; set; }
        public int TopicReplies { get; set; }
        public int TopicRepliesReal { get; set; }
        public byte TopicStatus { get; set; }
        public byte TopicType { get; set; }
        public int TopicFirstPostId { get; set; }
        public string TopicFirstPosterName { get; set; }
        public string TopicFirstPosterColour { get; set; }
        public int TopicLastPostId { get; set; }
        public int TopicLastPosterId { get; set; }
        public string TopicLastPosterName { get; set; }
        public string TopicLastPosterColour { get; set; }
        public string TopicLastPostSubject { get; set; }
        public long TopicLastPostTime { get; set; }
        public long TopicLastViewTime { get; set; }
        public int TopicMovedId { get; set; }
        public byte TopicBumped { get; set; }
        public int TopicBumper { get; set; }
        public string PollTitle { get; set; }
        public int PollStart { get; set; }
        public int PollLength { get; set; }
        public byte PollMaxOptions { get; set; }
        public int PollLastVote { get; set; }
        public byte PollVoteChange { get; set; }
    }
}
