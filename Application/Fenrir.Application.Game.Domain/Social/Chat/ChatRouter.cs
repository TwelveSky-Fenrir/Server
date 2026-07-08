namespace Fenrir.Application.Game.Domain.Social.Chat;

/// <summary>
///     Channel routing map for every chat-shaped packet, the single source of truth every
///     Handlers/Chat/*Handler implements against. In Fenrir's mono-GameServer topology the legacy's
///     ts25center relay collapses into a direct in-process fan-out: Zone.PostChatCommand for AOI/whole-zone
///     channels (Local/Shout/Tribe), ZoneRegistry directly for every cross-zone channel.
/// </summary>
/// <remarks>
///     Quirks worth knowing: Party chat's tLink field is dead server-side, never propagated, despite existing
///     on the wire; Guild chat's tLink IS transported; World chat refuses senders below level 10; GM notice
///     (opcode 17) requires the sender meet <c>GmCommandTier.Basic</c>
///     (<c>Fenrir.Network.Dispatch.Sessions.ZoneClientSession.MeetsGmTier</c>) and, uniquely among every
///     GM-gated command in Fenrir, never disconnects a denied sender -- see
///     <c>Fenrir.Application.Game.Abstractions.Chat.IGlobalAnnouncementService</c>'s own remarks.
/// </remarks>
public static class ChatRouter
{
    /// <summary>MAX_CHAT_CONTENT_LENGTH (DEFINE.h:608) -- every chat/notice Content field is exactly this many wire bytes.</summary>
    public const int MaxContentLength = 61;

    /// <summary>
    ///     Anti-fuzzing gate on every chat/notice handler. Party chat doesn't apply this -- it checks membership instead
    ///     and returns silently, not a Quit.
    /// </summary>
    public static bool IsContentEmpty(string content)
    {
        return string.IsNullOrEmpty(content);
    }

    /// <summary>
    ///     mServerNumber in {37, 119, 124, 84} -- Fenrir's Zone.MapId IS the legacy server number (one ts25zone process =
    ///     one map/server).
    /// </summary>
    public static bool IsShoutEnabledOnMap(short mapId)
    {
        return mapId is 37 or 119 or 124 or 84;
    }
}
