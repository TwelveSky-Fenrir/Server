using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_BUY_BLOOD_MARK_SEND (opcode 141). The client's submitted quantity is only a plausibility bound --
///     the quantity actually granted always comes from the catalog's own fixed <c>Quantity</c>.
/// </summary>
public sealed class BuyBloodMarkItemHandler(IBuyBloodMarkItemService service)
    : IAsyncPacketHandler<BuyBloodMarkItemRequest>
{
    public async ValueTask HandleAsync(BuyBloodMarkItemRequest packet, IPacketSession session,
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
            var result = await service.ResolveAndApplyAsync(packet, zone, state, characterId, cancellationToken);
            if (result is null)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(result.Value);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
