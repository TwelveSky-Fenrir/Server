using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class UseHotkeyItemHandler(IUseHotkeyItemService service, ILogger<UseHotkeyItemHandler> logger)
    : IAsyncPacketHandler<UseHotkeyItemRequest>
{
    public async ValueTask HandleAsync(UseHotkeyItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseHotkeyItemRequest received ({Page1}:{Index1})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseHotkeyItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        var page = packet.Page1;
        var index = packet.Index1;

        UseHotkeyItemOutcome outcome;
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            outcome = await service.UseAsync(zone, state, characterId, page, index, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        switch (outcome)
        {
            case UseHotkeyItemOutcome.Disconnect:
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
            case UseHotkeyItemOutcome.RejectedClean:
                session.Send(new UseHotkeyItemResponse { Result = 1, Page = page, Index = index });
                return;
            default:
                session.Send(new UseHotkeyItemResponse { Result = 0, Page = page, Index = index });
                return;
        }
    }
}
