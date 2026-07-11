using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The autonomous daily-reset broadcast (case 1600): center-originated in legacy, with no inbound zone
///     trigger at all. Fenrir has no separate always-on center process to host this on, so every shard runs
///     its own independent instance (driven by a periodic Hosting-layer <c>BackgroundService</c> -- see this
///     task's wiringManifest) -- harmless duplication since every shard's own wall clock reaches the identical 00:01
///     decision
///     independently and each shard only ever broadcasts to its own hosted players, exactly like every other
///     <c>BroadcastToEveryZone</c>-shaped call in this cluster already does per-shard.
/// </summary>
/// <remarks>
///     Réf. C++ (via the A12 scheduled-events behavior contract) : Server/ts25center/S07_MyGame01.cpp:199-238
///     (trigger, DB writes, broadcast send) ; :235 (payload explicitly zero-filled, unlike case 4000's
///     stale-buffer send) ; Server/ts25zone/S07_MyGame08.cpp:1323-1348 (zone-side receipt: iterates ready
///     users, clears in-memory reward-claim-state, and on a locally-cached Monday also clears reward-claim-day
///     -- both "moving zone" branches are byte-for-byte identical, confirmed dead/redundant branching, not
///     reproduced as two paths even if this class ever grows that behavior).
///     <para>
///         DELIBERATELY NOT REPRODUCED here: the DB-side reward-claim-day/reward-claim-state bulk reset
///         (contract Side effects, Daily-reset points 1 and 3). <c>game.usp_Character_GetRewardClaimState</c> /
///         <c>usp_Character_ClaimDailyReward</c>'s own comments document that this codebase already recomputes
///         the identical weekly reset LAZILY from the stored <c>RewardClaimDate</c> on every read/claim,
///         instead of requiring an active scheduled reset -- re-adding an active bulk DB write here would be
///         redundant with, and could race, that already-shipped lazy design. Fenrir also has no
///         <see cref="PlayerRuntimeState" /> in-memory mirror of those two DB fields for a zone-side reaction
///         to go stale in the first place (<c>ClaimDailyRewardService</c> re-reads fresh from the DB on every
///         claim), so the zone-side in-memory reset step has no analogous state to reset either. Only the
///         client-facing notice broadcast (whose client-side interpretation was not traced by this contract,
///         but "may still drive client-side presentation the server has no further role in" per its own Edge
///         cases) is reproduced.
///     </para>
/// </remarks>
public sealed class DailyResetBroadcaster(ZoneRegistry zones, ILogger<DailyResetBroadcaster> logger)
{
    private const int PayloadSize = 130;

    private readonly DailyResetBroadcastScheduler _scheduler = new();

    /// <summary>Call from a periodic host; broadcasts at most once per real occurrence of 00:01 UTC.</summary>
    public void Tick(DateTime utcNow)
    {
        if (_scheduler.TryConsumeDueFire(utcNow))
            Broadcast();
    }

    private void Broadcast()
    {
        var data = new byte[PayloadSize]; // zero-filled, matching the legacy send exactly (contract :235).
        var response = new ZoneEventInfoResponse
            { Sort = ScheduledZoneCenterEventCodes.DailyResetEventCode, Data = data };

        BroadcastToEveryZone(in response);

        logger.LogInformation("Autonomous daily-reset broadcast sent (sort {Sort})",
            ScheduledZoneCenterEventCodes.DailyResetEventCode);
    }

    /// <summary>Same rent-once/write-once/copy-N-times idiom as <see cref="ZoneEventBroadcaster" />'s own twin method.</summary>
    private void BroadcastToEveryZone<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                try
                {
                    if (player.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Daily-reset broadcast to character {RecipientId} (zone {MapId}) failed",
                        player.CharacterId, zone.MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
