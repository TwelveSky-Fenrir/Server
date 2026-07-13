using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.WorldState;

/// <summary>
/// Center-side authoritative state machine for the 11 Regular War (Zone049) instances. Advances each
/// instance through its phase cycle on a real wall-clock schedule and publishes the discrete phase value to
/// shards via <see cref="IWorldStateAuthority.SetZone049State"/>. The Center WRITES these phases; every
/// shard READS them as a fact (through its own <c>ZoneCenterBroadcastIngestor</c>, untouched by this unit).
/// </summary>
/// <remarks>
/// DELIBERATE DIVERGENCE FROM LEGACY (defensible, documented — not parity). In legacy <c>ts25center</c>, the
/// Center is purely reactive: it writes <c>mZone049TypeState[]</c> only when the owning shard notifies a
/// transition (<c>ZONE_BROADCAST_FOR_CENTER</c>, <c>tSort</c> 1..9); all timing/decision/scoring lives in the
/// shard's <c>Process_Zone_049_TYPE</c>. Fenrir moves the phase-timing authority into the Center (this class,
/// driven by <see cref="RegularWarPhaseHost"/>) for a cleaner single-writer world-state model — the target of
/// the shard→center authority switch (Lot 5). This lot the machine is DORMANT: it runs Center-side but shards
/// still author world state; nothing shard-side is removed.
/// <para>
/// THREADING. Single-writer. Every mutation happens inside <see cref="Advance"/> / <see cref="Initialize"/>,
/// which the host invokes serially from one loop. No locking is required or present; do not call
/// <see cref="Advance"/> from more than one thread.
/// </para>
/// </remarks>
public sealed class RegularWarPhaseAuthority
{
    private const int NoAnnouncement = -1;

    private static readonly int[] CountdownAnnounceMinutes = [10, 5, 1];

    private readonly IWorldStateAuthority _worldState;
    private readonly ILogger<RegularWarPhaseAuthority> _logger;
    private readonly Instance[] _instances;

    public RegularWarPhaseAuthority(
        IWorldStateAuthority worldState,
        ILogger<RegularWarPhaseAuthority> logger,
        TimeProvider? timeProvider = null)
    {
        _worldState = worldState;
        _logger = logger;

        var clock = timeProvider ?? TimeProvider.System;
        var baseline = clock.GetUtcNow();

        // Deterministic per-instance stagger so the 11 wars do NOT all fire in lockstep (a thundering herd of
        // 11 simultaneous RW maps). Legacy staggered implicitly via independent shard schedules; the contract
        // gives phase DURATIONS, not absolute time-of-day schedule points, so a free-running cycle with a
        // stagger is the honest reading. Absolute wall-clock schedule times remain a product decision.
        var stagger = RegularWarPhasePlan.DurationOf(RegularWarStage.Cooldown) / RegularWarInstanceMap.Count;

        _instances = new Instance[RegularWarInstanceMap.Count];
        for (var i = 0; i < _instances.Length; i++)
            _instances[i] = new Instance
            {
                Index = i,
                MapId = RegularWarInstanceMap.MapIdOf(i),
                Stage = RegularWarStage.Cooldown,
                StageEnteredAt = baseline - stagger * i,
                PublishedState = RegularWarPhasePlan.PublishedStateOf(RegularWarStage.Cooldown),
                LastAnnouncedRemainingMinute = NoAnnouncement
            };
    }

    /// <summary>
    /// Publishes the baseline phase value for every instance once, establishing the authoritative starting
    /// point downstream. Call before the first <see cref="Advance"/>.
    /// </summary>
    /// <remarks>
    /// Persistence/preload seam: today every instance starts at <see cref="RegularWarStage.Cooldown"/>. When
    /// the <c>worldstate-aggregates</c> unit wires <c>WORLD_INFO</c> persistence, a hydrated
    /// (<see cref="RegularWarPhaseSnapshot.Stage"/> + absolute <see cref="RegularWarPhaseSnapshot.StageEnteredAt"/>)
    /// per instance should replace the fresh-cooldown default here so a mid-cycle restart resumes correctly.
    /// </remarks>
    public void Initialize()
    {
        foreach (var instance in _instances)
            _worldState.SetZone049State(instance.Index, instance.PublishedState);

        _logger.LogInformation(
            "RegularWarPhaseAuthority initialized: {Count} Regular War instances published at baseline phase 0 " +
            "(Center-driven timing; shards read state==3 as war-active).",
            _instances.Length);
    }

    /// <summary>
    /// Advances every instance to the phase its wall-clock schedule dictates at <paramref name="now"/>,
    /// publishing each discrete phase change to shards. Catch-up safe (applies multiple transitions if the
    /// host stalled) and clock-hiccup safe (a zero/negative span advances nothing). Fault-isolated per
    /// instance — one instance faulting never skips the rest.
    /// </summary>
    /// <param name="now">The current monotonic wall-clock instant, supplied by the host's clock.</param>
    public void Advance(DateTimeOffset now)
    {
        foreach (var instance in _instances)
            try
            {
                AdvanceInstance(instance, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Regular War phase advance failed for instance {Index} (map {MapId}); its remaining and the " +
                    "other instances' transitions still run.",
                    instance.Index, instance.MapId);
            }
    }

    /// <summary>An immutable snapshot of every instance's current phase, for observability/tests/persistence.</summary>
    public ImmutableArray<RegularWarPhaseSnapshot> Snapshot()
    {
        var builder = ImmutableArray.CreateBuilder<RegularWarPhaseSnapshot>(_instances.Length);
        foreach (var instance in _instances)
            builder.Add(new RegularWarPhaseSnapshot(
                instance.Index, instance.MapId, instance.Stage, instance.PublishedState, instance.StageEnteredAt));

        return builder.MoveToImmutable();
    }

    private void AdvanceInstance(Instance instance, DateTimeOffset now)
    {
        var transitions = 0;

        while (true)
        {
            var duration = RegularWarPhasePlan.DurationOf(instance.Stage);
            var elapsed = now - instance.StageEnteredAt;

            // Not yet due (also covers a backwards clock hiccup: a negative span advances nothing).
            if (elapsed < duration)
                break;

            if (transitions >= RegularWarPhasePlan.MaxTransitionsPerAdvance)
            {
                _logger.LogWarning(
                    "Regular War instance {Index} (map {MapId}) hit the per-advance transition cap ({Cap}); the " +
                    "host is severely stalled. Remaining catch-up defers to the next advance.",
                    instance.Index, instance.MapId, RegularWarPhasePlan.MaxTransitionsPerAdvance);
                break;
            }

            var previousPublished = instance.PublishedState;
            var nextStage = RegularWarPhasePlan.NextOf(instance.Stage);

            instance.Stage = nextStage;
            instance.StageEnteredAt += duration; // carry the sub-duration remainder forward, no drift
            instance.LastAnnouncedRemainingMinute = NoAnnouncement;
            transitions++;

            var nextPublished = RegularWarPhasePlan.PublishedStateOf(nextStage);
            instance.PublishedState = nextPublished;

            if (nextPublished != previousPublished)
                PublishTransition(instance, nextStage, nextPublished);
        }

        if (instance.Stage == RegularWarStage.AnnounceCountdown)
            EmitCountdownAnnouncements(instance, now);
    }

    private void PublishTransition(Instance instance, RegularWarStage stage, int publishedState)
    {
        _worldState.SetZone049State(instance.Index, publishedState);

        switch (stage)
        {
            case RegularWarStage.WarActive:
                _logger.LogInformation(
                    "Regular War instance {Index} (map {MapId}) is now WAR ACTIVE (published phase 3).",
                    instance.Index, instance.MapId);
                break;
            case RegularWarStage.Cooldown:
                _logger.LogInformation(
                    "Regular War instance {Index} (map {MapId}) cycle complete; back to cooldown (published phase 0).",
                    instance.Index, instance.MapId);
                break;
            default:
                _logger.LogDebug(
                    "Regular War instance {Index} (map {MapId}) advanced to {Stage} (published phase {Phase}).",
                    instance.Index, instance.MapId, stage, publishedState);
                break;
        }
    }

    private void EmitCountdownAnnouncements(Instance instance, DateTimeOffset now)
    {
        var remaining = RegularWarPhasePlan.DurationOf(RegularWarStage.AnnounceCountdown) - (now - instance.StageEnteredAt);
        if (remaining <= TimeSpan.Zero)
            return;

        var remainingMinutes = (int)Math.Ceiling(remaining.TotalMinutes);
        if (remainingMinutes == instance.LastAnnouncedRemainingMinute)
            return;

        if (Array.IndexOf(CountdownAnnounceMinutes, remainingMinutes) < 0)
            return;

        instance.LastAnnouncedRemainingMinute = remainingMinutes;
        _logger.LogInformation(
            "Regular War instance {Index} (map {MapId}) announce countdown: {RemainingMinutes} minute(s) until entry opens.",
            instance.Index, instance.MapId, remainingMinutes);
    }

    private sealed class Instance
    {
        public required int Index { get; init; }
        public required short MapId { get; init; }
        public required RegularWarStage Stage { get; set; }
        public required DateTimeOffset StageEnteredAt { get; set; }
        public required int PublishedState { get; set; }
        public required int LastAnnouncedRemainingMinute { get; set; }
    }
}
