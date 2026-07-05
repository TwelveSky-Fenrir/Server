using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op127, CZ_UP_LEVEL_ITEM_SEND -- re-skins a cape into a randomly rolled higher tier (delegated to
///     <see cref="IUpgradeCapeService" />). Money is always deducted and the material always consumed regardless
///     of outcome; on failure the response's <c>Value</c> is all-zero (matches the legacy's own
///     zero-initialized, never-repopulated <c>tValue[6]</c> on that path -- NOT an echo of the original item).
/// </summary>
public sealed class UpgradeCapeHandler(IUpgradeCapeService upgradeCapeService)
    : IAsyncPacketHandler<UpgradeCapeRequest>
{
    public async ValueTask HandleAsync(UpgradeCapeRequest packet, IPacketSession session,
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
            var result = await upgradeCapeService.UpgradeAsync(packet, zone, state, characterId, cancellationToken);

            if (result.Outcome != UpgradeCapeOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new UpgradeCapeResponse
            {
                Result = result.Succeeded ? 0 : 1,
                Value = result.Value,
                Padding = 0
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
