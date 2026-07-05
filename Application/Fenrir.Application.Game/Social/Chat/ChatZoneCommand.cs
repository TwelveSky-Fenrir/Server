using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Social.Chat;

/// <summary>
///     The two chat channels that need Zone's own tick-owned AOI grid/player set to resolve their audience --
///     every other channel is a plain cross-zone or same-zone fan-out a handler can do directly.
/// </summary>
public enum ChatBroadcastKind : byte
{
    /// <summary>CZ_GENERAL_CHAT_SEND (38) -- AOI-neighbor broadcast, filtered by sender's tribe.</summary>
    Local,

    /// <summary>CZ_GENERAL_SHOUT_SEND (40) -- whole-zone broadcast, no tribe filter.</summary>
    Shout,

    /// <summary>CZ_TRIBE_CHAT_SEND (81) -- whole-zone broadcast, filtered by sender's tribe.</summary>
    Tribe
}

/// <summary>
///     Posted so Zone's own tick thread can resolve the audience (AOI grid for Local, live player set for
///     Shout/Tribe). Content/mute gating already happened in the handler, so this command is always a
///     pre-validated send.
/// </summary>
public readonly struct ChatZoneCommand
{
    public required int SenderCharacterId { get; init; }
    public required ChatBroadcastKind Kind { get; init; }
    public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
