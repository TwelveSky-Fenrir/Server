namespace Fenrir.Application.Game.Social.Chat;

/// <summary>
///     Channel routing map for every chat-shaped packet (contracts/02_chat_notices.md, 05_social.md),
///     the single source of truth every <c>Handlers/Chat/*Handler</c> implements against. In Fenrir's
///     mono-GameServer topology the legacy's ts25center relay (tSort 102-115) collapses into a direct
///     in-process fan-out: <c>Zone.PostChatCommand</c> for AOI/whole-zone channels (Local/Shout/Tribe),
///     <c>ZoneRegistry</c> directly for every cross-zone channel.
/// </summary>
/// <remarks>
///     | Channel (relay tSort) | CZ in / ZC out | Audience | Mute gate | Notes |
///     |---|---|---|---|---|
///     | Local (none -- zone-local) | 38 / 41 | AOI ±1 cell, same tribe (alliance not modeled) | YES | Sender always included (self-echo) |
///     | Whisper (103) | 39 / 42 | ONE resolved target, cross-zone (ts25playuser directory) | NO (not documented) | Result 0/1/3 three-way echo/deliver |
///     | Shout (none -- zone-local, server-number gated) | 40 / 43 | Whole zone | YES | Only zones/maps 37/119/124/84 |
///     | Party chat (105) | 68 / 76 | Party roster, ALL zones | NO (not documented) | tLink is DEAD server-side (report 02) -- never propagated |
///     | Guild chat (112) | 77 / 85 | Guild roster, ALL zones | YES | tLink IS transported |
///     | Guild notice (111) | 76 / 84 | Guild roster, ALL zones | NO | Guild-master only (aGuildRole DB role 2, GuildRoleCodec.IsMaster) |
///     | Tribe chat (none -- zone-local) | 81 / 90 | Whole zone, same tribe (alliance not modeled) | YES | No inter-zone relay |
///     | Tribe notice (113) | 80 / 89 | Same tribe, ALL zones | NO | Master/sub-master only (TribeRole 1 or 2) |
///     | Tribe notify/scroll (114) | 112 / 139 | Same tribe, ALL zones | NO | DEFERRED -- see TribeAnnouncementScrollHandler's own remarks (no scroll-count resource modeled) |
///     | World chat (115) | 152 / 192 | ALL zones, unfiltered | YES | Level &lt; 10 refused (Quit()) |
///     | GM notice (102) | 17 / 20 | ALL zones, unfiltered | n/a | Permanently inert -- no GM-rank concept modeled yet, see GlobalAnnouncementHandler |
/// </remarks>
public static class ChatRouter
{
    /// <summary><c>MAX_CHAT_CONTENT_LENGTH</c> (DEFINE.h:608) -- every chat/notice Content field is exactly this many wire bytes; an all-NUL payload decodes to "".</summary>
    public const int MaxContentLength = 61;

    /// <summary>Anti-fuzzing gate on every chat/notice handler (contracts: "contenu vide ⇒ Quit()"). Party chat doesn't apply this -- it checks membership instead and returns silently, not a Quit.</summary>
    public static bool IsContentEmpty(string content)
    {
        return string.IsNullOrEmpty(content);
    }

    /// <summary><c>mServerNumber ∈ {37, 119, 124, 84}</c> (CZ_GENERAL_SHOUT_SEND's own runtime gate, S04_MyWork02.cpp:8097) -- Fenrir's Zone.MapId IS the legacy server number (one ts25zone process = one map/server), verified identification.</summary>
    public static bool IsShoutEnabledOnMap(short mapId)
    {
        return mapId is 37 or 119 or 124 or 84;
    }
}
