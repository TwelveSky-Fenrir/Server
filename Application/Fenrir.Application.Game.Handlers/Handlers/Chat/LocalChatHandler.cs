using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_CHAT_SEND (opcode 38). A muted sender is silently dropped, not disconnected. GM
///     inline-command interception (where/ygdrop/lab/boss/kill200/?clear) is routed through
///     <see cref="ILocalChatService" /> -- see that interface's and
///     <see cref="Fenrir.Application.Game.Services.Chat.LocalChatService" />'s own remarks for which of the six
///     commands are actually implemented versus a tier-gated, logged no-op pending their own subsystem.
/// </summary>
public sealed class LocalChatHandler(ILocalChatService localChatService) : IInlinePacketHandler<LocalChatRequest>
{
    public void Handle(in LocalChatRequest packet, IPacketSession session)
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
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        localChatService.TryPostChat(zone, zoneSession, state, packet.Content, packet.Link);
    }
}
