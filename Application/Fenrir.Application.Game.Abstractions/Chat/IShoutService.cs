using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_GENERAL_SHOUT_SEND (opcode 40): the shout-enabled-map gate plus the mute gate,
///     then posting the whole-zone broadcast onto the sender's own zone tick.
/// </summary>
public interface IShoutService
{
    /// <summary>
    ///     Posts <paramref name="content" />/<paramref name="link" /> as a <see cref="ChatBroadcastKind.Shout" />
    ///     command onto <paramref name="zone" />'s tick. Silently ignored (returns false) outside
    ///     shout-enabled maps (<see cref="ChatRouter.IsShoutEnabledOnMap" />) or for a muted sender -- matches
    ///     the legacy's silent ignore, not a Quit.
    /// </summary>
    public bool TryPostShout(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
