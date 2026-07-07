using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_START_PSHOP_SEND (opcode 31). <c>Sort</c> 1 = live personal shop (a pure display overlay -- items
///     never leave <see cref="PlayerRuntimeState.Inventory" />), 2 = offline/deputy shop (items physically
///     leave into game.OfflineShopItems). Both sorts gated to zone 37.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:6067-6095 -- the cross-type "shop already open"
///     exclusivity gate (<see cref="OpenShopStallPrepareOutcome.Blocked" />, results 101/102/103), enforced
///     by <see cref="IOpenShopStallService.PrepareAsync" /> before either shop type is opened. The one
///     same-type case (a PERSONAL request while a PERSONAL shop is already open) instead disconnects the
///     session via <see cref="OpenShopStallPrepareOutcome.Abort" />, matching legacy's <c>Quit()</c>.
/// </remarks>
public sealed class OpenShopStallHandler(IOpenShopStallService service) : IAsyncPacketHandler<OpenShopStallRequest>
{
    /// <summary>Single source of truth: <see cref="ProxyShopZonePolicy.ZoneNumber" />; see its remarks.</summary>
    public const short PshopZoneNumber = ProxyShopZonePolicy.ZoneNumber;

    public async ValueTask HandleAsync(OpenShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var prepared = await service.PrepareAsync(packet, state, cancellationToken);
        switch (prepared.Outcome)
        {
            case OpenShopStallPrepareOutcome.Abort:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case OpenShopStallPrepareOutcome.LiveOpened:
            case OpenShopStallPrepareOutcome.Blocked:
                session.Send(prepared.Response!.Value);
                return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.OpenProxyShopAsync(packet, zone, state, characterId, accountId,
                prepared.Listing, prepared.OfflineItems!, cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
