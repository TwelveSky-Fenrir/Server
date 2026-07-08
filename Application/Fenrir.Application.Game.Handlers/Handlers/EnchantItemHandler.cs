using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op24, CZ_IMPROVE_ITEM_SEND -- normal-equipment/wings enchant (delegated to
///     <see cref="IEnchantItemService" />, which now resolves wings too -- see <c>EnchantResolver</c>'s
///     remarks). Costumes and stellar cores remain out of scope; those targets fall outside the resolver's
///     slot-type band and are reported as <see cref="EnchantItemOutcome.Rejected" />, which this handler
///     disconnects on, same as every other legacy Quit() condition in this cluster.
/// </summary>
/// <remarks>
///     <c>ProtectForDestroy</c> (Protection Charm) and <c>ImproveItemValue</c> ("sweet potato" Lucky Enchant
///     Scroll) both have real acquisition paths via <c>UseInventoryItemService</c> (op23) and are read from
///     live character state by <see cref="IEnchantItemService" /> -- <c>EnchantOutcome.Protected</c> is
///     reachable in production, not dead code. See <c>EnchantResolver</c>'s own remarks for the sweet-potato
///     bonus-probability magnitude, which is still not cited/applied.
/// </remarks>
public sealed class EnchantItemHandler(IEnchantItemService enchantItemService, ILogger<EnchantItemHandler> logger)
    : IAsyncPacketHandler<EnchantItemRequest>
{
    public async ValueTask HandleAsync(EnchantItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: EnchantItemRequest received ({Page1}:{Index1} + {Page2}:{Index2})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: EnchantItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        // Serializes the read/SQL/mirror sequence per character to close an item/money-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await enchantItemService.EnchantAsync(packet, zone, state, characterId, cancellationToken);

            if (result.Outcome == EnchantItemOutcome.Rejected)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
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
