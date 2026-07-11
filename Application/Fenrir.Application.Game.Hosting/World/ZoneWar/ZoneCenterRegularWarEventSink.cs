using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     The real, siege-state-driving <see cref="IRegularWarEventSink" /> that replaces the inert
///     <see cref="LoggingRegularWarEventSink" />: each Regular War (Zone049) lifecycle event
///     <see cref="RegularWarSchedulerHost" /> reports is translated into the matching Zone049 sub-code (1-9) and
///     fed to <see cref="ZoneCenterBroadcastIngestor.Ingest" />, which (1) writes the byte-exact center state
///     value into <see cref="ZoneCenterSiegeState" />'s Zone049 slot for this map, (2) relays the event
///     cluster-wide to every player this shard hosts, and (3) enqueues it onto the cross-shard rvr-siege relay
///     (Zone049 codes 1-9 all qualify) so every OTHER live shard converges on the same war status. This is the
///     "highest-value producer" the rvr-siege ingestion finding calls out: before it, <c>Ingest</c> had no
///     production caller at all (only tests).
///     <para>
///         <b>Sub-code mapping (the load-bearing part).</b> The <see cref="RegularWarSchedule" />'s own
///         <c>RegularWarPhase</c> (Idle=0, OpenGate=1, PreWar=2, Active=3, PostWarCleanup=4, ForcedReset=5) IS
///         the legacy Zone049 center state value (that class's own remarks establish this, and it matches the
///         center's code&#8594;state table at <c>S04_MyWork02.cpp:212-253</c>). This sink therefore maps each
///         event to the sub-code whose center-applied state equals the phase that event represents, inverting
///         that same table:
///         <list type="bullet">
///             <item>
///                 <see cref="OnCountdownAnnounced" /> &#8594; sub-code 1 (no state change; the countdown/
///                 Discord-diagnostic relay only, exactly as the legacy sub-code 1 behaves).
///             </item>
///             <item><see cref="OnActiveWarStarted" /> &#8594; sub-code 4 (&#8594; state 3, Active).</item>
///             <item>
///                 <see cref="OnWarConcluded" /> &#8594; sub-code 6 (&#8594; state 4, PostWarCleanup; the
///                 contract confirms sub-codes 6 and 7 are behaviorally identical, so the choice of 6 over 7 is
///                 immaterial).
///             </item>
///             <item>
///                 <see cref="OnAllSessionsShouldDisconnect" /> &#8594; sub-code 9 (&#8594; state 0, Idle -- the
///                 war is fully over and the slot returns to idle).
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Deliberate approximation, documented not hidden.</b> The <see cref="IRegularWarEventSink" /> surface
///         exposes no event for entering OpenGate (state 1), PreWar (state 2), or the transient ForcedReset
///         (state 5), so those intermediate micro-states are never surfaced through this sink -- the observable
///         Zone049 state sequence is idle&#8594;active(3)&#8594;post-war(4)&#8594;idle(0), which captures the war
///         lifecycle a cluster-wide siege-status display needs even though it skips the brief gate/pre-war/
///         forced-reset steps the legacy center would have shown. <see cref="OnSmallestTribeFlagged" /> (a
///         server-160 internal flag) and <see cref="OnMonstersShouldDespawn" /> (a monster-spawn concern) map to
///         no distinct Zone049 state code and so drive no <c>Ingest</c> -- surfacing them would require inventing
///         a producer&#8594;sub-code binding this cluster's contract does not supply.
///     </para>
///     <para>
///         <b>Security.</b> Every driver of this sink is <see cref="RegularWarSchedulerHost" />'s trusted 2 Hz
///         background timer -- no client packet reaches <c>Ingest</c> through this path, and the map id is
///         resolved to a slot only via the fixed <see cref="RegularWarMapCatalog" /> (never a client-supplied
///         value). <c>Ingest</c> additionally bounds-checks the slot defensively. See this cluster's report to
///         <c>fenrir-security-hardening-engineer</c>.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:212-253 (the Zone049 sub-code&#8594;state table this sink
///     inverts) ; Server/ts25zone/S07_MyGame01.cpp:4982-5034 (the Zone049 Regular War tick state machine whose
///     transitions the legacy producer emitted as sub-codes 1-9 -- reproduced here from
///     <see cref="RegularWarSchedule" />'s equivalent phase transitions).
/// </remarks>
public sealed class ZoneCenterRegularWarEventSink(
    ZoneCenterBroadcastIngestor ingestor,
    ILogger<ZoneCenterRegularWarEventSink> logger) : IRegularWarEventSink
{
    /// <summary>Sub-code 1: no state change -- the countdown/diagnostic relay only (S04_MyWork02.cpp:212).</summary>
    private const int CountdownAnnounceSubCode = 1;

    /// <summary>Sub-code 4 &#8594; state 3 (Active).</summary>
    private const int ActiveWarStartedSubCode = 4;

    /// <summary>Sub-code 6 &#8594; state 4 (PostWarCleanup); 6 and 7 are behaviorally identical.</summary>
    private const int WarConcludedSubCode = 6;

    /// <summary>Sub-code 9 &#8594; state 0 (Idle) -- the war is fully over.</summary>
    private const int WarEndedToIdleSubCode = 9;

    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
        if (!TryResolveSlot(mapId, out var slot))
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        WriteInt32(payload, 0, slot);

        // Sub-code 1's own remaining field (the legacy carried the remaining seconds; the center reads it only
        // for the Discord announcement and never for the state machine). Surfaced in the relayed payload for
        // client-side fidelity even though ApplyZone049 ignores it.
        WriteInt32(payload, 4, remainingMinutes);

        ingestor.Ingest(CountdownAnnounceSubCode, payload);
    }

    /// <summary>
    ///     A server-160 internal "smallest present tribe" flag, not a Zone049 phase-state transition -- no
    ///     distinct center state code, so no <c>Ingest</c>. See class remarks.
    /// </summary>
    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
    }

    public void OnActiveWarStarted(short mapId)
    {
        IngestForSlot(mapId, ActiveWarStartedSubCode);
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        IngestForSlot(mapId, WarConcludedSubCode);
    }

    /// <summary>
    ///     Monster despawn is a spawn-lifecycle concern, not a Zone049 center state code -- no <c>Ingest</c>. See
    ///     class remarks.
    /// </summary>
    public void OnMonstersShouldDespawn(short mapId)
    {
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
        IngestForSlot(mapId, WarEndedToIdleSubCode);
    }

    private void IngestForSlot(short mapId, int subCode)
    {
        if (!TryResolveSlot(mapId, out var slot))
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        WriteInt32(payload, 0, slot);
        ingestor.Ingest(subCode, payload);
    }

    private bool TryResolveSlot(short mapId, out int slot)
    {
        if (RegularWarMapCatalog.TryGet(mapId, out var config))
        {
            slot = config.WarSlotIndex;
            return true;
        }

        // Every RegularWarSchedulerHost-hosted map is in the catalog, so this is a defensive guard, not an
        // expected path -- a non-Regular-War map id must never index the Zone049 slot array.
        logger.LogWarning("RegularWar event for map {MapId} has no Zone049 slot in the catalog -- ignored", mapId);
        slot = 0;
        return false;
    }

    private static void WriteInt32(Span<byte> data, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data[offset..], value);
    }
}
