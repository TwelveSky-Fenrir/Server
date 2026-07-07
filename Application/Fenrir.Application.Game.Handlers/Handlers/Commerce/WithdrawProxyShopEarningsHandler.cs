using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110) -- withdraw accumulated offline-shop earnings into the
///     character's live money. Gated to zone 37, like its two sibling deputy-shop opcodes.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:13983-13992 -- <c>IsValidZoneForProxyShop</c> gate,
///     disconnecting the session (<c>Quit()</c>) on failure before any withdrawal processing runs. Identical
///     gate to <c>GET_DEPUTY_PSHOP_SEND</c> (:13961-13970, <see cref="GetProxyShopHandler" />) and
///     <c>SET_DEPUTY_PSHOP_SEND</c> (:13972-13981, <see cref="UpdateProxyShopHandler" />).
/// </remarks>
public sealed class WithdrawProxyShopEarningsHandler(IWithdrawProxyShopEarningsService service)
    : IAsyncPacketHandler<WithdrawProxyShopEarningsRequest>
{
    public async ValueTask HandleAsync(WithdrawProxyShopEarningsRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.WithdrawAsync(characterId, accountId, packet.Money, packet.BigMoney,
                cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
