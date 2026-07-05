using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110) -- withdraw accumulated offline-shop earnings into the
///     character's live money.
/// </summary>
public sealed class WithdrawProxyShopEarningsHandler(IWithdrawProxyShopEarningsService service)
    : IAsyncPacketHandler<WithdrawProxyShopEarningsRequest>
{
    public async ValueTask HandleAsync(WithdrawProxyShopEarningsRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.WithdrawAsync(characterId, packet.Money, packet.BigMoney,
                cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
