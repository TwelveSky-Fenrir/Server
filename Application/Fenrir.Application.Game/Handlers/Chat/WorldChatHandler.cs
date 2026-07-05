using Fenrir.Application.Game.Handlers.Chat.Services;
using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

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
