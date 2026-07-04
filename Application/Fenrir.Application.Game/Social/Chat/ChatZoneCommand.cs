using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Social.Chat;

/// <summary>The two chat channels that need <see cref="Zone" />'s own tick-owned AOI grid/player set to resolve their audience -- every other channel (whisper/party/guild/tribe/world/notices) is a plain cross-zone or same-zone fan-out a handler can do directly (see <c>Social.Chat.ChatRouter</c>'s own remarks).</summary>
public enum ChatBroadcastKind : byte
{
    /// <summary>CZ_GENERAL_CHAT_SEND (38) -- AOI-neighbor broadcast, filtered by sender's tribe (alliance not modeled -- see class remarks).</summary>
    Local,

    /// <summary>CZ_GENERAL_SHOUT_SEND (40) -- whole-zone broadcast, no tribe filter.</summary>
    Shout,

    /// <summary>CZ_TRIBE_CHAT_SEND (81) -- whole-zone broadcast, filtered by sender's tribe (alliance not modeled).</summary>
    Tribe
}

/// <summary>
///     Posted by <c>LocalChatHandler</c>/<c>ShoutHandler</c>/<c>TribeChatHandler</c> for the ONE thing
///     only <see cref="Zone" />'s own tick thread can safely resolve: the AOI-grid neighbor set (Local)
///     or the live player set (Shout/Tribe) -- both are tick-owned state (<c>Zone</c>'s own remarks on
///     <c>_grid</c>/<c>_players</c>), same "own channel, own drain" posture as <see cref="Combat.CombatCommand" />/
///     <c>Inventory.InventoryZoneCommand</c>. Content emptiness/mute gating already happened in the
///     handler (needs only the SENDER's own state, which the handler already has via
///     <c>Zone.TryGetPlayer</c>) -- this command is always a legitimate, already-cleared send.
/// </summary>
public readonly struct ChatZoneCommand
{
    public required int SenderCharacterId { get; init; }
    public required ChatBroadcastKind Kind { get; init; }
    public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
