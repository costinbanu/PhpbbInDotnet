using System;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class CacheUtility
    {
        public static string GetPostAttachmentsCacheKey(int postId, Guid correlationId)
            => $"PostAttachments_{postId}_{correlationId}";

        public static string GetAttachmentCacheKey(int attachId, Guid correlationId)
            => $"AttachmentDto_{attachId}_{correlationId}";

        public static string GetAvatarCacheKey(int userId, Guid correlationId)
            => $"Avatar_{userId}_{correlationId}";
    }
}
