using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op24, CZ_IMPROVE_ITEM_SEND -- standard equipment enchant (delegated to <see cref="IEnchantItemService" />).
///     Wings, costumes and stellar cores are out of scope and reply with a clean failure rather than a
///     disconnect.
/// </summary>
/// <remarks>
///     <c>ProtectForDestroy</c> has no acquisition path yet, so <c>EnchantResolver.Resolve</c> is always called
///     with 0 charges and its <c>Protected</c> outcome is currently unreachable.
/// </remarks>
public sealed class EnchantItemHandler(IEnchantItemService enchantItemService)
    : IAsyncPacketHandler<EnchantItemRequest>
{
    public async ValueTask HandleAsync(EnchantItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item/money-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await enchantItemService.EnchantAsync(packet, zone, state, characterId, cancellationToken);

            switch (result.Outcome)
            {
                case EnchantItemOutcome.Rejected:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                case EnchantItemOutcome.NotSupported:
                    session.Send(new EnchantItemResponse { Result = 1, Cost = 0, Value = 0 });
                    return;
            }

            session.Send(new EnchantItemResponse
            {
                Result = result.ResultCode, Cost = result.Cost, Value = result.NewEnchant
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
