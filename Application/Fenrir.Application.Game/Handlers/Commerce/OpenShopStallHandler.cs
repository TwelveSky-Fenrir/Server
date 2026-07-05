using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_START_PSHOP_SEND (opcode 31). <c>Sort</c> 1 = live personal shop (a pure display overlay -- items
///     never leave <see cref="PlayerRuntimeState.Inventory" />), 2 = offline/deputy shop (items physically
///     leave into game.OfflineShopItems). Both sorts gated to zone 37.
/// </summary>
public sealed class OpenShopStallHandler(IOpenShopStallService service) : IAsyncPacketHandler<OpenShopStallRequest>
{
    public const short PshopZoneNumber = 37;

    public async ValueTask HandleAsync(OpenShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var prepared = service.Prepare(packet, state);
        switch (prepared.Outcome)
        {
            case OpenShopStallPrepareOutcome.Abort:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case OpenShopStallPrepareOutcome.LiveOpened:
                session.Send(prepared.LiveResponse!.Value);
                return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.OpenProxyShopAsync(packet, zone, state, characterId, prepared.Listing,
                prepared.OfflineItems!, cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
