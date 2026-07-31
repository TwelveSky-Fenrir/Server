using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Center.WorldState;

public sealed class RegularWarPhaseAuthority
{
    private const int NoAnnouncement = -1;

    private static readonly int[] CountdownAnnounceMinutes = [10, 5, 1];
    private readonly Instance[] _instances;
    private readonly ILogger<RegularWarPhaseAuthority> _logger;

    private readonly IWorldStateAuthority _worldState;

    public RegularWarPhaseAuthority(
        IWorldStateAuthority worldState,
        ILogger<RegularWarPhaseAuthority> logger,
        TimeProvider? timeProvider = null)
    {
        _worldState = worldState;
        _logger = logger;

        var clock = timeProvider ?? TimeProvider.System;
        var baseline = clock.GetUtcNow();

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

    public void Initialize()
    {
        foreach (var instance in _instances)
            _worldState.SetZone049State(instance.Index, instance.PublishedState);

        _logger.LogInformation(
            "RegularWarPhaseAuthority initialized: {Count} Regular War instances published at baseline phase 0 " +
            "(Center-driven timing; shards read state==3 as war-active).",
            _instances.Length);
    }

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
            instance.StageEnteredAt += duration;
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
        var remaining = RegularWarPhasePlan.DurationOf(RegularWarStage.AnnounceCountdown) -
                        (now - instance.StageEnteredAt);
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
