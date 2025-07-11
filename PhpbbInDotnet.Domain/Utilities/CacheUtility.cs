using System;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class CacheUtility
    {
        public static string GetAttachmentCacheKey(int attachId, int postId)
            => $"AttachmentDto_{attachId}_{postId}";

        public static string GetAvatarCacheKey(int userId, Guid correlationId)
            => $"Avatar_{userId}_{correlationId}";
    }
}
