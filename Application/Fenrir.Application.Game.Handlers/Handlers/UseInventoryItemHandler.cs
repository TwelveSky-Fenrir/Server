using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class UseInventoryItemHandler(IUseInventoryItemService service, ILogger<UseInventoryItemHandler> logger)
    : IAsyncPacketHandler<UseInventoryItemRequest>
{
    public async ValueTask HandleAsync(UseInventoryItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest received ({Page}:{Index}, value {Value})",
                zoneSession.SessionId, characterId, packet.Page, packet.Index, packet.Value);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        var page = packet.Page;
        var index = packet.Index;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index))
        {
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest aborted, invalid slot ({Page}:{Index})",
                zoneSession.SessionId, characterId, page, index);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var now = DateTime.UtcNow;
        if (now - state.LastItemUseUtc < SimulationClock.LegacyTick)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest rejected, anti-flood gate ({Page}:{Index})",
                zoneSession.SessionId, characterId, page, index);
            session.Send(new UseInventoryItemResponse
                { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 });
            return;
        }

        state.LastItemUseUtc = now;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.ResolveAsync(zone, state, characterId, accountId, (byte)page, (byte)index,
                packet.Value, cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
