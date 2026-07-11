using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The per-zone-tick driver for the Zone195 "Nok-San" solo point-capture war
///     (<c>Process_Zone_195</c>, Server/ts25zone/S07_MyGame01.cpp:8385-8602) -- a genuine
///     <see cref="ISimulationSystem" /> (same shape as <see cref="World.ZoneWar.ValleyWarSystem" />/
///     <see cref="World.ZoneWar.Zone335FfaEventCycleSystem" />, not a separate Hosting-level timer) because
///     every side effect runs directly on the affected zone's own tick thread: a candidate scan of that
///     zone's own <see cref="Zone.Players" />, cluster-wide op94 status broadcasts, CP/hero-point reward
///     grants, and the atomic stone flip into the process-wide <see cref="Zone195NokSanState" />.
/// </summary>
/// <remarks>
///     <para>
///         Only maps configured as a Nok-San stone shard in <see cref="Zone195NokSanSiteCatalog" /> ever do
///         real work -- every other zone is a cheap catalog-miss no-op. The legacy ships exactly three stone
///         shards (servers 196/99/100 -> slots 0/2/3, Server/ts25zone/S07_MyGame01.cpp:1140-1176); an operator
///         configures which hosted map id each corresponds to. The catalog is empty by default, so the feature
///         is fully dormant until configured.
///     </para>
///     <para>
///         The four-state machine (idle-and-searching -> settle -> countdown -> commit,
///         <see cref="Zone195CaptureMachine" />) is held per-map in <see cref="_machines" />; each instance is
///         only ever touched from its own map's single tick thread, so no locking is needed on the machine
///         itself -- <see cref="ConcurrentDictionary{TKey,TValue}" /> guards only the first-ever
///         get-or-create race between two DIFFERENT maps' genuinely-concurrent tick threads (same posture as
///         <see cref="ValleyWarKillRegistry" />).
///     </para>
///     <para>
///         <b>Documented gaps, not silent ones:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             The candidate/re-validation eligibility contract also names "not hidden" and "ready" checks
///             (Server/ts25zone/S07_MyGame01.cpp:8388-8408). Neither has a backing
///             <see cref="PlayerRuntimeState" /> flag today (same gap
///             <see cref="World.ZoneWar.HolyStoneWarCycle" />/<see cref="World.Monsters.MonsterAiSystem" />
///             already document for their own hidden-state checks), so only the modelled checks -- not zoning
///             (<see cref="PlayerRuntimeState.IsMovingZone" />), not dead
///             (<see cref="PlayerRuntimeState.IsDead" />), not in the disqualifying action state
///             (<see cref="PlayerRuntimeState.ActionSort" /> == <see cref="DisqualifyingActionSort" />), and
///             inside the capture radius -- are applied. "Ready" is treated as "present in
///             <see cref="Zone.Players" /> and not zoning/dead".
///         </item>
///         <item>
///             The client-facing broadcast payloads' exact wire byte layout (the character-name fields in
///             particular) is a documented wire-format gap -- see <see cref="IZone195NokSanBroadcaster" />.
///         </item>
///         <item>
///             The stone state is process-local, not yet cross-shard -- see <see cref="Zone195NokSanState" />.
///         </item>
///     </list>
/// </remarks>
public sealed class Zone195NokSanSystem(
    Zone195NokSanSiteCatalog sites,
    Zone195NokSanState stoneState,
    Lazy<IZone195NokSanBroadcaster> broadcaster,
    HeroRankPointAccumulator heroRankPoints,
    ILogger<Zone195NokSanSystem> logger,
    Func<DateTime>? utcNow = null) : ISimulationSystem
{
    /// <summary>Remaining-time counter value at lock (Server/ts25zone/S07_MyGame01.cpp:8385-8420).</summary>
    public const int CaptureRemainingStart = 5;

    /// <summary>
    ///     Settle delay before the countdown begins: one tenth of a game-minute
    ///     (Server/ts25zone/S07_MyGame01.cpp:8450-8459). A game-minute is 120 legacy ticks
    ///     (Server/Header/function.h:1643's default 1.0 game-minute, MinuteCountdown's 120-ticks-per-minute
    ///     assumption), so 0.1 x 120 = 12 legacy ticks (~6 s at TimeLogic=500ms).
    /// </summary>
    public const int SettleLegacyTicks = 12;

    /// <summary>
    ///     Countdown broadcast/decrement interval: one game-minute = 120 legacy ticks (~60 s)
    ///     (Server/ts25zone/S07_MyGame01.cpp:8479-8492, Server/Header/function.h:1643).
    /// </summary>
    public const int CountdownIntervalLegacyTicks = 120;

    /// <summary>
    ///     The blanket "not eligible right now" action-sort value treated identically to hidden/dead in both
    ///     candidate selection and continuous re-validation (Server/ts25zone/S07_MyGame01.cpp:8395-8398,8439,8469).
    ///     Its precise in-game meaning is not established by the contract -- flagged for cpp-zone-gameplay-analyst
    ///     if byte-for-byte semantics are needed.
    /// </summary>
    public const int DisqualifyingActionSort = 33;

    /// <summary>Capturer time-bonus CP grant (Server/ts25zone/S07_MyGame01.cpp:8494-8524).</summary>
    public const int CapturerContributionPoints = 50;

    /// <summary>Capturer time-bonus hero-rank point grant (Server/ts25zone/S07_MyGame01.cpp:8494-8524).</summary>
    public const int CapturerHeroPoints = 50;

    /// <summary>Nearby same-tribe ally time-bonus CP grant, each (Server/ts25zone/S07_MyGame01.cpp:8494-8524).</summary>
    public const int AllyContributionPoints = 10;

    /// <summary>Nearby same-tribe ally time-bonus hero-rank point grant, each (Server/ts25zone/S07_MyGame01.cpp:8494-8524).</summary>
    public const int AllyHeroPoints = 10;

    /// <summary>
    ///     Radius (world units) around the capture post within which same-tribe allies receive the time-bonus
    ///     reward (Server/ts25zone/S07_MyGame01.cpp:8494-8524). Measured on the X/Z plane, matching
    ///     <see cref="World.ZoneWar.HolyStoneWarCycle" />'s own participation-radius check and the AOI grid's
    ///     own X/Z partitioning.
    /// </summary>
    public const float AllyRewardRadius = 1000f;

    /// <summary>
    ///     Combined-level floor for hero-rank point accrual -- <c>LV_M1</c> (Server/Header/Protocol/DEFINE.h:451,
    ///     Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:782-785). A lower-level capturer/ally still receives
    ///     the contribution-point half of the reward, but not the hero-rank half.
    /// </summary>
    public const int HeroRankPointMinimumCombinedLevel = 113;

    /// <summary>Time-bonus window start hour (inclusive), game-clock (Server/ts25zone/S07_MyGame01.cpp:294-305).</summary>
    public const int RewardWindowStartHour = 20;

    /// <summary>Time-bonus window end hour (inclusive) -- hours 20 and 21, i.e. the 20:00-21:59 window.</summary>
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
                // Commit is transient and applied inline in the tick that reaches it -- it must never persist
                // between ticks. Defensive reset if somehow observed here.
                logger.LogWarning("Zone195 Nok-San map {MapId} observed in transient Commit phase; resetting",
                    zone.MapId);
                machine.ResetToIdle();
                break;
        }
    }

    /// <summary>Idle -> settle: lock onto the first eligible, non-owning-tribe challenger inside the capture radius.</summary>
    private void ScanForChallenger(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine)
    {
        var holderTribe = stoneState.GetOwningTribe(site.StoneSlotIndex); // null = uncaptured -> nobody excluded
        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;

        foreach (var player in zone.Players)
        {
            if (!IsEligibleCandidate(player, site, captureRadiusSq))
                continue;
            if (holderTribe is { } holder && player.Tribe == holder)
                continue; // the owning tribe cannot challenge its own stone

            machine.Phase = Zone195CapturePhase.Settle;
            machine.CapturerCharacterId = player.CharacterId;
            machine.CapturerTribe = player.Tribe;
            machine.CapturerName = player.Name;
            machine.RemainingTime = CaptureRemainingStart;
            machine.PhaseAccumulatorTicks = 0;

            broadcaster.Value.AnnounceChallengerAppeared(player.Tribe, player.Name);
            return;
        }

        // No eligible candidate -- stay idle, broadcast nothing (contract §edge case "No eligible candidate present").
    }

    private void AdvanceSettle(Zone zone, Zone195NokSanSite site, Zone195CaptureMachine machine, int legacyTicksElapsed)
    {
        if (!RevalidateCapturer(zone, site, machine))
            return;

        machine.PhaseAccumulatorTicks += legacyTicksElapsed;
        if (machine.PhaseAccumulatorTicks < SettleLegacyTicks)
            return;

        // Settle elapsed: emit the first countdown broadcast with the current remaining value, decrement, and
        // move to the countdown state (Server/ts25zone/S07_MyGame01.cpp:8450-8459).
        machine.PhaseAccumulatorTicks -= SettleLegacyTicks;
        broadcaster.Value.AnnounceCountdown(machine.RemainingTime, site.LegacyServerNumber);
        machine.RemainingTime--;
        machine.Phase = Zone195CapturePhase.Countdown;

        // A multi-tick burst that overran the settle threshold immediately feeds the countdown intervals with
        // the leftover accumulator -- burst-tolerant, same posture as every other catch-up countdown here.
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

    /// <summary>
    ///     Drains whole one-game-minute intervals from the accumulator: while remaining time is above zero,
    ///     emit a countdown broadcast with the current value and decrement it; the interval that finds remaining
    ///     time already at zero instead commits the capture (Server/ts25zone/S07_MyGame01.cpp:8479-8492).
    /// </summary>
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
            return; // machine is back to idle; any further accumulator belongs to the next challenger's cycle
        }
    }

    /// <summary>
    ///     Commit (transient): emit the success broadcast, grant the time-bonus reward if the window is open,
    ///     flip the stone atomically, broadcast the new authoritative state, and reset to idle -- all inline in
    ///     this single tick (Server/ts25zone/S07_MyGame01.cpp:8485-8602).
    /// </summary>
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

    /// <summary>
    ///     Re-validates the locked capturer every tick during settle/countdown. On any failure -- the capturer
    ///     left, died, hid, zoned out, entered the disqualifying action state, or stepped outside the radius --
    ///     emits the "capture cancelled" broadcast and resets fully to idle (captured progress is not
    ///     preserved), returning <see langword="false" /> (Server/ts25zone/S07_MyGame01.cpp:8433-8448,8463-8478).
    /// </summary>
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

    /// <summary>
    ///     Time-bonus reward (LNW33 build, only when the window is open): the capturer receives +50 CP and +50
    ///     hero points; every OTHER online, ready, same-tribe character within <see cref="AllyRewardRadius" />
    ///     of the capture post receives +10 CP and +10 hero points each
    ///     (Server/ts25zone/S07_MyGame01.cpp:8494-8524).
    /// </summary>
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
            if (player.IsMovingZone || player.IsDead) // "ready" (present + not zoning/dead); "not hidden" not modelled
                continue;

            var dx = player.PosX - site.PostX;
            var dz = player.PosZ - site.PostZ;
            if (dx * dx + dz * dz > allyRadiusSq)
                continue;

            GrantReward(zone, player, AllyContributionPoints, AllyHeroPoints);
        }
    }

    /// <summary>
    ///     CP is always granted (floored at zero + persisted) via the established <c>ProcessForCP</c> primitive
    ///     <see cref="Zone.GrantContributionPoints" />; hero-rank points accrue only for combined level
    ///     >= <see cref="HeroRankPointMinimumCombinedLevel" /> (LV_M1), mirroring
    ///     <c>Zone.ApplyPvpKillHeroPoints</c>: the live <see cref="PlayerRuntimeState.HeroRankPoints" /> mirror
    ///     is updated synchronously and the durable write-behind delta is queued through
    ///     <see cref="HeroRankPointAccumulator" />.
    /// </summary>
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
        // Only the "server 196" shard (stone slot 0) can ever open the window; only on Sunday, hours 20-21
        // (Server/ts25zone/S07_MyGame01.cpp:274,294-305). Evaluated on demand at commit rather than via a
        // separate per-minute recompute -- behaviourally identical for the purpose of the reward gate.
        if (!site.IsRewardWindowShard)
            return false;

        var now = _utcNow();
        return now.DayOfWeek == DayOfWeek.Sunday
               && now.Hour >= RewardWindowStartHour
               && now.Hour <= RewardWindowEndHour;
    }

    private static bool IsEligibleCandidate(PlayerRuntimeState player, Zone195NokSanSite site, float captureRadiusSq)
    {
        if (player.IsMovingZone) // zoning
            return false;
        if (player.IsDead)
            return false;
        if (player.ActionSort == DisqualifyingActionSort)
            return false;
        // "not hidden" and the explicit "ready" flag have no backing PlayerRuntimeState state -- documented gap.

        var dx = player.PosX - site.PostX;
        var dz = player.PosZ - site.PostZ;
        return dx * dx + dz * dz <= captureRadiusSq;
    }

    private static DateTime DefaultNowUtc()
    {
        return DateTime.UtcNow;
    }
}
