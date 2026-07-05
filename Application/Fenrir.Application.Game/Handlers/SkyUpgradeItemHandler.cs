using Fenrir.Application.Game.Enchant;
using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op93, CZ_SKY_UP_ITEM_SEND -- Warlord-item-only upgrade (delegated to <see cref="ISkyUpgradeItemService" />).
///     Money is always deducted and the material always consumed regardless of outcome (matches the legacy's
///     own unconditional <c>wAvatar.aMoney -= tCost</c>/<c>DecreaseMaterial</c> placement before the roll).
/// </summary>
public sealed class SkyUpgradeItemHandler(ISkyUpgradeItemService skyUpgradeItemService)
    : IAsyncPacketHandler<SkyUpgradeItemRequest>
{
    public async ValueTask HandleAsync(SkyUpgradeItemRequest packet, IPacketSession session,
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
            var result = await skyUpgradeItemService.UpgradeAsync(packet, zone, state, characterId,
                cancellationToken);

            if (result.Outcome != SkyUpgradeItemOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new SkyUpgradeItemResponse
            {
                Result = result.Succeeded ? 0 : 1,
                Cost = SkyUpgradeResolver.Cost,
                Value = result.Value
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
