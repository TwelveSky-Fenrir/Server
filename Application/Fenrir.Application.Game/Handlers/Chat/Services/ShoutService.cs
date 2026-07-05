using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

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
    bool TryPostShout(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}

public sealed class ShoutService : IShoutService
{
    public bool TryPostShout(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (!ChatRouter.IsShoutEnabledOnMap(zone.MapId))
            return false;

        if (sender.IsMuted)
            return false;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Shout,
            Content = content,
            Link = link
        });

        return true;
    }
}
