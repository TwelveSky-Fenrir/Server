using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class WorldChatHandler(IWorldChatService worldChatService) : IInlinePacketHandler<WorldChatRequest>
{
    public void Handle(in WorldChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        var outcome = worldChatService.TrySendChat(sender, packet.Content);
        if (outcome == WorldChatOutcome.LevelTooLow)
            zoneSession.Abort(DisconnectReason.Faulted);
    }
}
