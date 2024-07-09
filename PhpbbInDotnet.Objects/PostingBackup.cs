using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public record PostingBackup(PostingActions PostingActions, string? Title, string? Text, DateTime TextTime, int ForumId, int? TopicId, int? PostId, 
        List<int>? AttachmentIds, bool QuotePostInDifferentTopic);
}
