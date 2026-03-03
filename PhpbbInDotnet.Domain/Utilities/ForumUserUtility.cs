namespace PhpbbInDotnet.Domain.Utilities;

public static class ForumUserUtility
{
    public static bool IsValidRegisteredUserId(int userId)
        => userId > 0 && userId != Constants.ANONYMOUS_USER_ID;
}
