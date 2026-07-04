using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_SHOUT_SEND (opcode 40). Silently ignored outside shout-enabled maps
///     (<see cref="ChatRouter.IsShoutEnabledOnMap" />) -- matches the legacy's silent ignore, not a Quit.
/// </summary>
public sealed class ShoutHandler : IInlinePacketHandler<ShoutRequest>
{
    public void Handle(in ShoutRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone || !ChatRouter.IsShoutEnabledOnMap(zone.MapId))
            return;

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null || state.IsMuted)
            return;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = characterId,
            Kind = ChatBroadcastKind.Shout,
            Content = packet.Content,
            Link = packet.Link
        });
    }
}
