namespace Fenrir.Application.Game.Domain.Social.Chat;

public static class ChatRouter
{
    public const int MaxContentLength = 61;

    public static bool IsContentEmpty(string content)
    {
        return string.IsNullOrEmpty(content);
    }

    public static bool IsShoutEnabledOnMap(short mapId)
    {
        return mapId is 37 or 119 or 124 or 84;
    }
}
