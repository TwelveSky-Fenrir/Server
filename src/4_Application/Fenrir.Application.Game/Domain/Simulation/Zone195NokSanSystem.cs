using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class Zone195NokSanSystem(
    Zone195NokSanSiteCatalog sites,
    Zone195NokSanState stoneState,
    Lazy<IZone195NokSanBroadcaster> broadcaster,
    HeroRankPointAccumulator heroRankPoints,
    ILogger<Zone195NokSanSystem> logger,
    Func<DateTime>? utcNow = null) : ISimulationSystem
{
    public const int CaptureRemainingStart = 5;

    public const int SettleLegacyTicks = 12;

    public const int CountdownIntervalLegacyTicks = 120;

    public const int DisqualifyingActionSort = 33;

    public const int CapturerContributionPoints = 50;

    public const int CapturerHeroPoints = 50;

    public const int AllyContributionPoints = 10;

    public const int AllyHeroPoints = 10;

    public const float AllyRewardRadius = 1000f;

    public const int HeroRankPointMinimumCombinedLevel = 113;

    public const int RewardWindowStartHour = 20;

    public const int RewardWindowEndHour = 21;

    private readonly ConcurrentDictionary<short, Zone195CaptureMachine> _machines = new();
    private readonly Func<DateTime> _utcNow = utcNow ?? DefaultNowUtc;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0 || !sites.TryGet(zone.MapId, out var site) || site is null)
            return;

        var machine = _machines.GetOrAdd(zone.MapId, static _ => new Zone195CaptureMachine());

        switch (machine.Phase)
        {
            case Zone195CapturePhase.IdleSearching:
                ScanForChallenger(zone, site, machine);
                break;
            case Zone195CapturePhase.Settle:
                AdvanceSettle(zone, site, machine, legacyTicksElapsed);
                break;
            case Zone195CapturePhase.Countdown:
                AdvanceCountdown(zone, site, machine, legacyTicksElapsed);
                break;
            case Zone195CapturePhase.Commit:
                logger.LogWarning("Zone195 Nok-San map {MapId} observed in transient Commit phase; resetting",
                    zone.MapId);
                machine.ResetToIdle();
                break;
        }
    }

    private void ScanForChallenger(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine)
    {
        var holderTribe = stoneState.GetOwningTribe(site.StoneSlotIndex);
        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;

        foreach (var player in zone.Players)
        {
            if (!IsEligibleCandidate(player, site, captureRadiusSq))
                continue;
            if (holderTribe is { } holder && player.Tribe == holder)
                continue;

            machine.Phase = Zone195CapturePhase.Settle;
            machine.CapturerCharacterId = player.CharacterId;
            machine.CapturerTribe = player.Tribe;
            machine.CapturerName = player.Name;
            machine.RemainingTime = CaptureRemainingStart;
            machine.PhaseAccumulatorTicks = 0;

            broadcaster.Value.AnnounceChallengerAppeared(player.Tribe, player.Name);
            return;
        }
    }

    private void AdvanceSettle(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine, int legacyTicksElapsed)
    {
        if (!RevalidateCapturer(zone, site, machine))
            return;

        machine.PhaseAccumulatorTicks += legacyTicksElapsed;
        if (machine.PhaseAccumulatorTicks < SettleLegacyTicks)
            return;

        machine.PhaseAccumulatorTicks -= SettleLegacyTicks;
        broadcaster.Value.AnnounceCountdown(machine.RemainingTime, site.LegacyServerNumber);
        machine.RemainingTime--;
        machine.Phase = Zone195CapturePhase.Countdown;

        ProcessCountdownIntervals(zone, site, machine);
    }

    private void AdvanceCountdown(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine,
        int legacyTicksElapsed)
    {
        if (!RevalidateCapturer(zone, site, machine))
            return;

        machine.PhaseAccumulatorTicks += legacyTicksElapsed;
        ProcessCountdownIntervals(zone, site, machine);
    }

    private void ProcessCountdownIntervals(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine)
    {
        while (machine.PhaseAccumulatorTicks >= CountdownIntervalLegacyTicks)
        {
            machine.PhaseAccumulatorTicks -= CountdownIntervalLegacyTicks;

            if (machine.RemainingTime > 0)
            {
                broadcaster.Value.AnnounceCountdown(machine.RemainingTime, site.LegacyServerNumber);
                machine.RemainingTime--;
                continue;
            }

            CommitCapture(zone, site, machine);
            return;
        }
    }

    private void CommitCapture(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine)
    {
        machine.Phase = Zone195CapturePhase.Commit;
        var winningTribe = machine.CapturerTribe;

        broadcaster.Value.AnnounceCaptureSucceeded(winningTribe, site.LegacyServerNumber, machine.CapturerName);

        if (IsRewardWindowOpen(site))
            GrantTimeBonusRewards(zone, site, machine.CapturerCharacterId, winningTribe);

        stoneState.CommitCapture(site.StoneSlotIndex, winningTribe);
        broadcaster.Value.AnnounceNokSanState(winningTribe, site.LegacyServerNumber, stoneState.Snapshot());

        machine.ResetToIdle();
    }

    private bool RevalidateCapturer(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine)
    {
        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;

        if (machine.CapturerCharacterId != Zone195CaptureMachine.NoCapturer
            && zone.TryGetPlayer(machine.CapturerCharacterId, out var capturer)
            && capturer is not null
            && IsEligibleCandidate(capturer, site, captureRadiusSq))
            return true;

        broadcaster.Value.AnnounceCaptureCancelled(site.LegacyServerNumber);
        machine.ResetToIdle();
        return false;
    }

    private void GrantTimeBonusRewards(Zone zone, Zone195NokSanSite site, int capturerId, byte winningTribe)
    {
        if (zone.TryGetPlayer(capturerId, out var capturer) && capturer is not null)
            GrantReward(zone, capturer, CapturerContributionPoints, CapturerHeroPoints);

        var allyRadiusSq = AllyRewardRadius * AllyRewardRadius;
        foreach (var player in zone.Players)
        {
            if (player.CharacterId == capturerId)
                continue;
            if (player.Tribe != winningTribe)
                continue;
            if (player.IsMovingZone || player.IsDead)
                continue;

            var dx = player.PosX - site.PostX;
            var dz = player.PosZ - site.PostZ;
            if (dx * dx + dz * dz > allyRadiusSq)
                continue;

            GrantReward(zone, player, AllyContributionPoints, AllyHeroPoints);
        }
    }

    private void GrantReward(Zone zone, PlayerRuntimeState player, int contributionPoints, int heroPoints)
    {
        zone.GrantContributionPoints(player.CharacterId, contributionPoints);

        if (player.CombinedLevel < HeroRankPointMinimumCombinedLevel)
            return;

        player.HeroRankPoints += heroPoints;
        heroRankPoints.AddPending(player.CharacterId, heroPoints, player.Tribe, player.Level);
    }

    private bool IsRewardWindowOpen(Zone195NokSanSite site)
    {
        if (!site.IsRewardWindowShard)
            return false;

        var now = _utcNow();
        return now.DayOfWeek == DayOfWeek.Sunday
               && now.Hour >= RewardWindowStartHour
               && now.Hour <= RewardWindowEndHour;
    }

    private static bool IsEligibleCandidate(PlayerRuntimeState player, Zone195NokSanSite site, float captureRadiusSq)
    {
        if (player.IsMovingZone)
            return false;
        if (player.IsDead)
            return false;
        if (player.ActionSort == DisqualifyingActionSort)
            return false;

        var dx = player.PosX - site.PostX;
        var dz = player.PosZ - site.PostZ;
        return dx * dx + dz * dz <= captureRadiusSq;
    }

    private static DateTime DefaultNowUtc()
    {
        return DateTime.UtcNow;
    }
}
