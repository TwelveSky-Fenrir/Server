using System.Buffers;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     A11 -- the periodic per-player tower-war info push (legacy per-player maintenance pass,
///     S07_MyGame04.cpp:835-837,882-884). The legacy fires <c>SendTowerInfo</c> once per player on a strict
///     60-tick "30-second" cadence (<c>== 60</c>); Fenrir's 500 ms legacy tick makes 60 legacy ticks exactly
///     30 seconds, which resolves the source contract's flagged tick-rate uncertainty. Modeled here as one
///     zone-level 30 s cadence that pushes the current 12-tower snapshot to every player in the tower's own zone,
///     rather than per-player-staggered timers -- a deliberate simplification of the legacy's per-player
///     <c>mTickCountFor01Minute</c>-style counters (a keep-alive snapshot is idempotent, so the exact per-player
///     phase does not matter). Gated to tower-hosting zones, matching the legacy precondition.
///     <para>
///         Driven from <see cref="Progression.TowerInfoPushSystem" /> (an <c>ISimulationSystem</c>) so the cadence
///         is counted in the zone's own tick thread; the accrual counter below lives per-<see cref="Zone" /> (never
///         on the shared singleton system) exactly like every other per-zone countdown accrual field. The push
///         itself reuses this zone's own <c>towerWar</c> and the same rent-once/write-once/copy-N-times broadcast
///         idiom as <c>Zone.Combat.BroadcastTowerStatus</c>.
///     </para>
///     <para>
///         The exact <c>SendTowerInfo</c> wire payload (S07_MyGame03.cpp:9716) was not opened for the source
///         contract, so this reuses the already-implemented <see cref="TowerStatusResponse" />
///         (<c>B_BROADCAST_CHUGSOUNG_INFO</c>, the full 12-tower ownership/status snapshot) -- flagged for
///         confirmation that <c>SendTowerInfo</c> carries that same layout.
///     </para>
/// </summary>
public partial class Zone
{
    /// <summary>60 legacy ticks * 500 ms = 30 s (contract 60-tick "30-second" per-player push cadence).</summary>
    private const int TowerInfoPushLegacyTicks = 60;

    private int _towerInfoPushAccrualTicks;

    /// <summary>
    ///     Accrues elapsed legacy ticks and, once the 30-second cadence is reached, pushes the current 12-tower
    ///     snapshot to every player in this zone. No-op on a non-tower zone, when this zone has no tower state
    ///     wired, or when no players are present. Called once per tick from <see cref="Progression.TowerInfoPushSystem" />.
    /// </summary>
    internal void TickTowerInfoPush(int legacyTicksElapsed)
    {
        if (towerWar is null || legacyTicksElapsed <= 0)
            return;

        if (TowerZoneIndexTable.GetTowerIndex(MapId) < 0)
            return;

        _towerInfoPushAccrualTicks += legacyTicksElapsed;
        if (_towerInfoPushAccrualTicks < TowerInfoPushLegacyTicks)
            return;

        // Reset (not subtract): a keep-alive push only ever needs the latest snapshot, so a multi-tick catch-up
        // burst after a stall still pushes exactly once rather than N times.
        _towerInfoPushAccrualTicks = 0;

        if (_players.IsEmpty)
            return;

        var response = towerWar.BuildStatusSnapshot();

        // Rent-once/write-once/copy-N-times -- same idiom as Zone.Combat.BroadcastTowerStatus: the frame is
        // encoded exactly once and copied into each recipient's own transport, with a per-recipient failure
        // isolated (logged, skipped) rather than aborting the rest of this zone-wide push.
        var total = FrameWriter.FrameSizeOf<TowerStatusResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var player in _players.Values)
                try
                {
                    if (player.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} periodic tower-info push to character {RecipientId} failed",
                        MapId, player.CharacterId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
