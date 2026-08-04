using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class Zone195NokSanSystem(
    Zone195NokSanSiteCatalog sites,
    Zone195NokSanState stoneState,
    Lazy<IZone195NokSanBroadcaster> broadcaster,
    HeroRankPointAccumulator heroRankPoints,
    ILogger<Zone195NokSanSystem> logger,
    TimeProvider? timeProvider = null) : ISimulationSystem
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

    private const int HeroRankPointStatSort = 904;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0 || !stoneState.IsInitialized || !sites.TryGet(zone.MapId, out var site) ||
            site is null)
            return;

        if (stoneState.TryDequeueConfirmedCapture(zone.MapId, out var confirmedCapture))
        {
            PublishConfirmedCapture(zone, site, confirmedCapture);
            return;
        }

        if (!stoneState.TryGetCaptureSnapshot(zone.MapId, out var capture))
            return;

        switch (capture.Phase)
        {
            case Zone195CapturePhase.IdleSearching:
                ScanForChallenger(zone, site);
                break;
            case Zone195CapturePhase.Settle:
                AdvanceSettle(zone, site, capture, legacyTicksElapsed);
                break;
            case Zone195CapturePhase.Countdown:
                AdvanceCountdown(zone, site, capture, legacyTicksElapsed);
                break;
            case Zone195CapturePhase.Commit:
                logger.LogWarning("Zone195 Nok-San map {MapId} persisted an invalid transient Commit phase",
                    zone.MapId);
                break;
        }
    }

    private void ScanForChallenger(Zone zone, Zone195NokSanSite site)
    {
        var holderTribe = stoneState.GetOwningTribe(site.StoneSlotIndex);
        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;

        foreach (var player in zone.Players)
        {
            if (!IsEligibleCandidate(player, site, captureRadiusSq) ||
                (holderTribe is { } holder && player.Tribe == holder))
                continue;

            var appeared = false;
            stoneState.TryMutateCapture(zone.MapId, machine =>
            {
                if (machine.Phase != Zone195CapturePhase.IdleSearching)
                    return false;

                machine.Phase = Zone195CapturePhase.Settle;
                machine.CapturerCharacterId = player.CharacterId;
                machine.CapturerTribe = player.Tribe;
                machine.CapturerName = player.Name;
                machine.RemainingTime = CaptureRemainingStart;
                machine.PhaseAccumulatorTicks = 0;
                appeared = true;
                return true;
            });

            if (appeared)
                broadcaster.Value.AnnounceChallengerAppeared(player.Tribe, player.Name);

            return;
        }
    }

    private void AdvanceSettle(Zone zone, Zone195NokSanSite site, Zone195NokSanCaptureSnapshot capture,
        int legacyTicksElapsed)
    {
        if (!RevalidateCapturer(zone, site, capture))
        {
            CancelCapture(zone.MapId, site, capture);
            return;
        }

        AdvanceCaptureClock(zone, site, capture, legacyTicksElapsed, Zone195CapturePhase.Settle);
    }

    private void AdvanceCountdown(Zone zone, Zone195NokSanSite site, Zone195NokSanCaptureSnapshot capture,
        int legacyTicksElapsed)
    {
        if (capture.RemainingTime == 0 && stoneState.GetOwningTribe(site.StoneSlotIndex) == capture.CapturerTribe)
            return;

        if (!RevalidateCapturer(zone, site, capture))
        {
            CancelCapture(zone.MapId, site, capture);
            return;
        }

        AdvanceCaptureClock(zone, site, capture, legacyTicksElapsed, Zone195CapturePhase.Countdown);
    }

    private void AdvanceCaptureClock(Zone zone, Zone195NokSanSite site, Zone195NokSanCaptureSnapshot capture,
        int legacyTicksElapsed, Zone195CapturePhase expectedPhase)
    {
        List<int>? countdowns = null;
        var shouldComplete = false;

        stoneState.TryMutateCapture(zone.MapId, machine =>
        {
            if (!Matches(capture, machine, expectedPhase))
                return false;

            machine.PhaseAccumulatorTicks = checked(machine.PhaseAccumulatorTicks + legacyTicksElapsed);
            if (machine.Phase == Zone195CapturePhase.Settle)
            {
                if (machine.PhaseAccumulatorTicks < SettleLegacyTicks)
                    return true;

                machine.PhaseAccumulatorTicks -= SettleLegacyTicks;
                (countdowns ??= []).Add(machine.RemainingTime);
                machine.RemainingTime--;
                machine.Phase = Zone195CapturePhase.Countdown;
            }

            while (machine.PhaseAccumulatorTicks >= CountdownIntervalLegacyTicks)
            {
                machine.PhaseAccumulatorTicks -= CountdownIntervalLegacyTicks;
                if (machine.RemainingTime > 0)
                {
                    (countdowns ??= []).Add(machine.RemainingTime);
                    machine.RemainingTime--;
                    continue;
                }

                shouldComplete = true;
                break;
            }

            return true;
        });

        if (countdowns is not null)
            foreach (var remainingTime in countdowns)
                broadcaster.Value.AnnounceCountdown(remainingTime, site.LegacyServerNumber);

        if (shouldComplete)
            CompleteCapture(zone, site, capture.CapturerCharacterId);
    }

    private void CompleteCapture(Zone zone, Zone195NokSanSite site, int expectedCapturerCharacterId)
    {
        if (!stoneState.TryCompleteCapture(zone.MapId, site.StoneSlotIndex, expectedCapturerCharacterId,
                out _, out _))
            return;
    }

    private void PublishConfirmedCapture(Zone zone, Zone195NokSanSite site,
        in Zone195NokSanConfirmedCapture confirmedCapture)
    {
        var winningTribe = confirmedCapture.CapturerTribe;
        broadcaster.Value.AnnounceCaptureSucceeded(winningTribe, site.LegacyServerNumber,
            confirmedCapture.CapturerName);

        if (site.IsRewardWindowShard && Zone195TimeEventGate.IsOpenAt(_timeProvider))
            GrantTimeBonusRewards(zone, site, confirmedCapture.CapturerCharacterId, winningTribe);

        broadcaster.Value.AnnounceNokSanState(winningTribe, site.LegacyServerNumber, confirmedCapture.State);
        logger.LogWarning(
            "Zone195 Nok-San capture on map {MapId} was committed locally but has no idempotent durable world-event delivery contract; cross-shard publication is disabled.",
            zone.MapId);
    }

    private void CancelCapture(short mapId, Zone195NokSanSite site, Zone195NokSanCaptureSnapshot capture)
    {
        var cancelled = false;
        stoneState.TryMutateCapture(mapId, machine =>
        {
            if (!Matches(capture, machine, capture.Phase))
                return false;

            machine.ResetToIdle();
            cancelled = true;
            return true;
        });

        if (cancelled)
            broadcaster.Value.AnnounceCaptureCancelled(site.LegacyServerNumber);
    }

    private static bool Matches(in Zone195NokSanCaptureSnapshot expected, Zone195CaptureMachine actual,
        Zone195CapturePhase expectedPhase)
    {
        return actual.Phase == expectedPhase && actual.CapturerCharacterId == expected.CapturerCharacterId &&
               actual.CapturerTribe == expected.CapturerTribe && actual.CapturerName == expected.CapturerName &&
               actual.RemainingTime == expected.RemainingTime &&
               actual.PhaseAccumulatorTicks == expected.PhaseAccumulatorTicks;
    }

    private bool RevalidateCapturer(Zone zone, Zone195NokSanSite site,
        in Zone195NokSanCaptureSnapshot capture)
    {
        if (stoneState.GetOwningTribe(site.StoneSlotIndex) is { } holder && holder == capture.CapturerTribe)
            return false;

        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;
        return capture.CapturerCharacterId != Zone195CaptureMachine.NoCapturer &&
               zone.TryGetPlayer(capture.CapturerCharacterId, out var capturer) && capturer is not null &&
               capturer.Tribe == capture.CapturerTribe && IsEligibleCandidate(capturer, site, captureRadiusSq);
    }

    private void GrantTimeBonusRewards(Zone zone, Zone195NokSanSite site, int capturerId, byte winningTribe)
    {
        if (zone.TryGetPlayer(capturerId, out var capturer) && capturer is not null)
            GrantReward(zone, capturer, CapturerContributionPoints, CapturerHeroPoints);

        var allyRadiusSq = AllyRewardRadius * AllyRewardRadius;
        foreach (var player in zone.Players)
        {
            if (player.CharacterId == capturerId || player.Tribe != winningTribe || player.IsMovingZone ||
                player.IsDead)
                continue;

            var dx = player.PosX - site.PostX;
            var dz = player.PosZ - site.PostZ;
            if (dx * dx + dz * dz <= allyRadiusSq)
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
        player.Session.Send(new AvatarStatUpdateResponse
            { Sort = HeroRankPointStatSort, Value = player.HeroRankPoints, Value2 = 0 });
    }

    private static bool IsEligibleCandidate(PlayerRuntimeState player, Zone195NokSanSite site, float captureRadiusSq)
    {
        if (player.IsMovingZone || player.IsDead || player.ActionSort == DisqualifyingActionSort)
            return false;

        var dx = player.PosX - site.PostX;
        var dz = player.PosZ - site.PostZ;
        return dx * dx + dz * dz <= captureRadiusSq;
    }
}
