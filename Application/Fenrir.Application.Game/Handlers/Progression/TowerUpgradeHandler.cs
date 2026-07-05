using Fenrir.Application.Game.Handlers.Progression.Services;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_CHUGSOUNG_WAR_UP_SEND (opcode 120). Every gate before the herb/bar lookup disconnects on
///     failure, matching the legacy's own Quit()-only error handling here. <c>FindInventoryItem</c>'s own
///     contract (S04_MyWork05.cpp:4867) never returns a hit with a -1 page/index, so the caller's
///     subsequent "page/index == -1" checks (S04_MyWork02.cpp:14396-14403) are dead code in the traced
///     source -- ZC_CHUGSOUNG_WAR_UP_RECV's Result=1/2 ("missing herb"/"missing bar") values are never
///     actually reachable; a missing herb or bar disconnects instead, same as every earlier gate.
///     <para>
///         Material consumption only arms the upgrade (<see cref="TowerWarState.BeginUpgrade" />); the actual
///         level change, guardian-monster respawn, and the later siege/destruction cycle all run off that
///         tower's own zone tick -- see <see cref="TowerGuardianSystem" />.
///     </para>
/// </summary>
public sealed class TowerUpgradeHandler(ITowerUpgradeService towerUpgradeService)
    : IAsyncPacketHandler<TowerUpgradeRequest>
{
    public async ValueTask HandleAsync(TowerUpgradeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await towerUpgradeService.UpgradeAsync(characterId, zone, state, packet, cancellationToken);

            if (result.Outcome != TowerUpgradeOutcome.Success)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new TowerUpgradeResponse
            {
                Result = 0, Page = [result.PackedPage, 0], Index = [result.PackedIndex, 0], Count = 1
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
