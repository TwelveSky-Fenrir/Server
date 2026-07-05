using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_WORLD_CHAT_SEND (opcode 152). Level &lt; 10 aborts (anti-spam-bot gate); muted senders are
///     silently dropped. Broadcasts to every zone, unfiltered. The wire's <c>TribeRole</c> field for this
///     opcode is actually the sender's tribe number, not a role -- passed through verbatim.
/// </summary>
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
