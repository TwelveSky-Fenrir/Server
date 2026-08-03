namespace Fenrir.Application.Game.Domain.Social.Chat;

public static class ChatRouter
{
    public const int MaxContentLength = 61;

    public const int MaxAvatarNameLength = 13;

    public static string SafeString(string value, int fieldWidth)
    {
        return value.Length < fieldWidth ? value : value[..(fieldWidth - 1)];
    }

    public static string SafeContent(string content)
    {
        return SafeString(content, MaxContentLength);
    }

    public static string SafeAvatarName(string avatarName)
    {
        return SafeString(avatarName, MaxAvatarNameLength);
    }

    public static bool IsContentEmpty(string content)
    {
        return string.IsNullOrEmpty(content);
    }

    public static bool IsShoutEnabledOnMap(short mapId)
    {
        return mapId is 37 or 119 or 124 or 84;
    }
}
