namespace PhpbbInDotnet.Domain.Utilities
{
    public static class ForumLinkUtility
	{
        public static string IndexPage = "../Index";

        public static string GetRelativeUrlToForum(int forumId)
            => $"./ViewForum?forumId={forumId}";

        public static (string Url, object RouteValues) GetRedirectObjectToForum(int forumId)
            => ("../ViewForum", new { forumId });

        public static string GetAbsoluteUrlToForum(string baseUrl, int forumId)
            => $"{baseUrl.TrimEnd("/.".ToCharArray())}/{GetRelativeUrlToForum(forumId).TrimStart("/.".ToCharArray())}";

        public static string GetRelativeUrlToTopic(int topicId, int pageNum)
            => $"./ViewTopic?topicId={topicId}&pageNum={pageNum}";

        public static (string Url, object RouteValues) GetRedirectObjectToTopic(int topicId, int pageNum)
            => ("../ViewTopic", new { topicId, pageNum });

        public static string GetAbsoluteUrlToTopic(string baseUrl, int topicId, int pageNum)
            => $"{baseUrl.TrimEnd("/.".ToCharArray())}/{GetRelativeUrlToTopic(topicId, pageNum).TrimStart("/.".ToCharArray())}";

        public static string GetRelativeUrlToPost(int postId)
            => $"./ViewTopic?postId={postId}&handler=ByPostId";

        public static (string Url, object RouteValues) GetRedirectObjectToPost(int postId)
            => ("../ViewTopic", new { postId, handler = "ByPostId" });

        public static string GetAbsoluteUrlToPost(string baseUrl, int postId)
            => $"{baseUrl.TrimEnd("/.".ToCharArray())}/{GetRelativeUrlToPost(postId).TrimStart("/.".ToCharArray())}";

        public static (string Url, object RouteValues) GetRedirectObjectToFile(int fileId)
            => ($"../File", new { id = fileId });

        public static (string Url, object RouteValues) GetRedirectObjectToAvatar(int userId)
            => ($"../File", new { userId, handler = "Avatar" });
    }
}
