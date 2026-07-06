using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Where <see cref="HolyStoneWarCycle" /> currently sits in its cooldown/open/contest/challenge cycle.</summary>
public enum HolyStoneWarPhase : byte
{
    /// <summary>
    ///     Between wars -- counting down <see cref="HolyStoneWarCycle.CooldownRemaining" />, frozen while the Tribe
    ///     Symbol Battle window is open.
    /// </summary>
    Cooldown,

    /// <summary>Cooldown elapsed; counting the ten-simulated-minute "opens in N minutes" countdown plus one final minute.</summary>
    OpeningCountdown,

    /// <summary>War zone open; scanning every tick for the first eligible challenger.</summary>
    Contest,

    /// <summary>A challenger was found; re-validating them every tick while their five-simulated-minute countdown runs.</summary>
    ChallengePending
}

/// <summary>
///     Fixed geometry/identity for the one map the Holy Stone itself sits on -- not named by the translated
///     behavior contract (only that a "fixed radius" and a "fixed location" exist), so this must be supplied
///     by configuration rather than guessed. See <see cref="HolyStoneWarCycle" />'s own remarks.
/// </summary>
public sealed record HolyStoneWarSite(
    short MapId,
    float StoneX,
    float StoneZ,
    float CaptureRadius,
    float ParticipationRadius);

/// <summary>
///     Zone038's Holy Stone possession war cycle: a cooldown between wars, a ten-simulated-minute-plus-one
///     opening countdown, a contest phase that scans for the first eligible challenger within
///     <see cref="HolyStoneWarSite.CaptureRadius" /> of the Stone, a five-simulated-minute per-candidate
///     challenge countdown re-validated every tick, and -- on success -- possession change, reward
///     distribution, quest-progress advance, guard spawn, then back to cooldown.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:4015-4287 (full state machine body) ; :2878-2893 (per-tick
///     gate, boot-time server-number==38 gate -- translated as "whichever live shard hosts
///     <c>GameServerOptions.HolyStoneMapId</c>" by
///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.HolyStoneWarCycleHost</c>, this class's own driver;
///     this class itself has no shard-election concept, it simply always evaluates the configured
///     <see cref="HolyStoneWarSite" />) ; :793-819 (war-counter pre-set-so-no-immediate-war
///     behavior -- reproduced here simply by always starting in <see cref="HolyStoneWarPhase.Cooldown" />,
///     never straight into a war, on construction) ; Server/ts25latest_config.props:10-11,
///     Server/Header/Protocol/DEFINE.h:21-30 (both reward-gating feature switches active simultaneously in the
///     shipped build, confirming the two capture-reward grants are additive, not alternatives -- see
///     <see cref="IHolyStoneCaptureRewardGateway.GrantCaptureReward" />'s own remarks) ;
///     Server/ts25zone/S07_MyGame01.cpp:3114-3117 (tribe-or-declared-ally "already holds the Stone" helper,
///     shared with Zone039 -- see <see cref="HolyStoneTribeMatch" />) ; Server/Header/function.h:1642-1652
///     (tick-to-minute conversion, see <see cref="MinuteCountdown" />'s own remarks).
///     <para>
///         GAP -- not modeled, deliberately not guessed, all requiring a fresh legacy-behavior-translator
///         finding before they can be implemented for real: (1) every interim broadcast this cycle produces
///         (opening countdown, "war zone now open", "challenger approaches", "challenger left/failed") has no
///         cited wire sort/opcode -- logged only; only the final capture notice reuses the already-modeled
///         sort 38 via <see cref="ZoneEventBroadcaster.AnnounceZone038Winner" />, and even that call's payload
///         carries only the winning tribe id, not the capturing character's name the contract also calls for;
///         (2) the previous/new holder's per-tribe "bonus value" (contract: cleared/logged on loss, a fresh
///         1-3 random value assigned/logged on capture) has no backing field anywhere in
///         <see cref="WorldStateService" /> -- the random roll is computed and logged here but not persisted,
///         pending a schema addition from a database-engineer/database-schema owner; (3) candidate
///         eligibility only checks what <see cref="PlayerRuntimeState" /> actually models today (not dead,
///         tribe/ally vs. holder, radius) -- the contract's "not hidden," "not already in the specific
///         busy/claiming action state," "still visible," and "not in either of two specific disqualifying
///         action states" checks are skipped outright, since no such flags exist on
///         <see cref="PlayerRuntimeState" /> yet and the contract does not name the exact ActionSort/flag
///         values involved; (4) reward amounts and the quest marker id are the
///         <see cref="IHolyStoneCaptureRewardGateway" /> seam -- see its own remarks (guard spawn, contract
///         step 9, is NOT part of that seam: it is already real production behavior, see that interface's own
///         remarks).
///     </para>
/// </remarks>
public sealed class HolyStoneWarCycle(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ZoneRegistry zones,
    IHolyStoneCaptureRewardGateway rewardGateway,
    ILogger<HolyStoneWarCycle> logger,
    HolyStoneWarSite site,
    IRandomSource? random = null,
    bool testMode = false)
{
    /// <summary>Ten simulated minutes of "opens in N minutes" countdown steps, plus one final minute before opening.</summary>
    public const int OpeningCountdownMinutes = 10;

    /// <summary>Five simulated minutes a selected challenger must survive re-validation to capture the Stone.</summary>
    public const int ChallengeCountdownMinutes = 5;

    /// <summary>Normal between-war cooldown: three simulated hours.</summary>
    public static readonly TimeSpan NormalCooldown = TimeSpan.FromHours(3);

    /// <summary>Test-mode between-war cooldown: one simulated hour.</summary>
    public static readonly TimeSpan TestModeCooldown = TimeSpan.FromHours(1);

    private readonly MinuteCountdown _minuteCountdown = new();
    private readonly IRandomSource _random = random ?? SystemRandomSource.Instance;

    /// <summary>Always starts here -- see class remarks on the war-counter pre-set behavior this reproduces.</summary>
    public HolyStoneWarPhase Phase { get; private set; } = HolyStoneWarPhase.Cooldown;

    public TimeSpan CooldownRemaining { get; private set; } = testMode ? TestModeCooldown : NormalCooldown;

    /// <summary>Non-null only while <see cref="Phase" /> is <see cref="HolyStoneWarPhase.ChallengePending" />.</summary>
    public int? PendingCandidateCharacterId { get; private set; }

    public void Tick(TimeSpan elapsed)
    {
        switch (Phase)
        {
            case HolyStoneWarPhase.Cooldown:
                AdvanceCooldown(elapsed);
                break;
            case HolyStoneWarPhase.OpeningCountdown:
                AdvanceOpeningCountdown(elapsed);
                break;
            case HolyStoneWarPhase.Contest:
                ScanForCandidate();
                break;
            case HolyStoneWarPhase.ChallengePending:
                AdvanceChallenge(elapsed);
                break;
        }
    }

    private void AdvanceCooldown(TimeSpan elapsed)
    {
        // Mutually exclusive with Zone037: no new war may begin while the Tribe Symbol Battle window is open --
        // the cooldown simply stops progressing rather than resetting.
        if (worldState.World.TribeSymbolBattle)
            return;

        CooldownRemaining -= elapsed;
        if (CooldownRemaining > TimeSpan.Zero)
            return;

        Phase = HolyStoneWarPhase.OpeningCountdown;
        _minuteCountdown.Reset();
        logger.LogInformation("HolyStoneWar: cooldown elapsed -- opening countdown armed");
    }

    private void AdvanceOpeningCountdown(TimeSpan elapsed)
    {
        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            var minute = _minuteCountdown.MinutesElapsed;

            if (minute is >= 1 and <= OpeningCountdownMinutes)
            {
                var remaining = OpeningCountdownMinutes + 1 - minute;
                // GAP: no cited wire sort for this interim notice -- logged only, see class remarks.
                logger.LogInformation("HolyStoneWar: opens in {MinutesRemaining} minute(s)", remaining);
                continue;
            }

            if (minute != OpeningCountdownMinutes + 1)
                continue;

            // GAP: no cited wire sort for the "war zone now open" notice -- logged only, see class remarks.
            logger.LogInformation("HolyStoneWar: war zone now open -- contest phase begins");
            Phase = HolyStoneWarPhase.Contest;
            return;
        }
    }

    private void ScanForCandidate()
    {
        if (!zones.TryGet(site.MapId, out var zone) || zone is null)
            return;

        var holderTribe = worldState.World.Zone038WinTribe;
        var allyOfHolder = holderTribe is { } holder ? worldState.GetAllyOf(holder) : null;
        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;

        foreach (var player in zone.Players)
        {
            if (player.IsDead)
                continue;
            if (HolyStoneTribeMatch.Matches(player.Tribe, holderTribe, allyOfHolder))
                continue; // this tribe already holds (or is allied with the holder of) the Stone
            if (DistanceSquared(player.PosX, player.PosZ, site.StoneX, site.StoneZ) > captureRadiusSq)
                continue;

            PendingCandidateCharacterId = player.CharacterId;
            Phase = HolyStoneWarPhase.ChallengePending;
            _minuteCountdown.Reset();
            // GAP: no cited wire sort for the "challenger approaches" notice -- logged only, see class remarks.
            logger.LogInformation(
                "HolyStoneWar: challenger {CharacterId} (tribe {Tribe}) approaches -- {Minutes}-minute countdown begins",
                player.CharacterId, player.Tribe, ChallengeCountdownMinutes);
            return;
        }
    }

    private void AdvanceChallenge(TimeSpan elapsed)
    {
        if (PendingCandidateCharacterId is not { } candidateId || !zones.TryGet(site.MapId, out var zone) ||
            zone is null)
        {
            CancelChallenge();
            return;
        }

        var captureRadiusSq = site.CaptureRadius * site.CaptureRadius;

        if (!zone.TryGetPlayer(candidateId, out var candidate) || candidate is null || candidate.IsDead ||
            DistanceSquared(candidate.PosX, candidate.PosZ, site.StoneX, site.StoneZ) > captureRadiusSq)
        {
            CancelChallenge();
            return;
        }

        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
            if (_minuteCountdown.MinutesElapsed >= ChallengeCountdownMinutes)
            {
                ResolveCapture(zone, candidate);
                return;
            }
    }

    private void CancelChallenge()
    {
        // GAP: no cited wire sort for the "challenger left/failed" notice -- logged only, see class remarks.
        logger.LogInformation("HolyStoneWar: challenger {CharacterId} left/failed -- resuming scan",
            PendingCandidateCharacterId);

        PendingCandidateCharacterId = null;
        Phase = HolyStoneWarPhase.Contest;
    }

    private void ResolveCapture(Zone zone, PlayerRuntimeState capturer)
    {
        var previousHolder = worldState.World.Zone038WinTribe;
        if (previousHolder is { } previous)
            // GAP: no backing "bonus" field to actually clear -- logged only, see class remarks.
            logger.LogInformation("HolyStoneWar: clearing bonus for previous holder tribe {PreviousHolderTribe}",
                previous);

        // GAP: sort 38's payload carries only the winning tribe id, not the capturing character's name -- see
        // class remarks. Sets WorldStateService.World.Zone038WinTribe and broadcasts sort 38 in one call --
        // which also forces the winning tribe's zone038-winner guard pool to resummon on every zone
        // (TribeGuardSpawner.ForceZone038WinnerResummon, wired inside AnnounceZone038Winner itself), satisfying
        // contract step 9 for real with no separate call needed here.
        logger.LogInformation(
            "HolyStoneWar: new holder tribe {WinningTribe}, captured by {CharacterName} ({CharacterId})",
            capturer.Tribe, capturer.Name, capturer.CharacterId);
        broadcaster.AnnounceZone038Winner(capturer.Tribe);

        // GAP: no backing field to persist this bonus to -- logged only, see class remarks.
        var bonus = _random.NextInt32(3) + 1;
        logger.LogInformation(
            "HolyStoneWar: new holder tribe {WinningTribe} bonus rolled {Bonus} (not persisted -- no backing field yet)",
            capturer.Tribe, bonus);

        rewardGateway.GrantCaptureReward(capturer);
        GrantTribeWideParticipationRewards(zone, capturer);
        AdvanceQuestProgressServerWide(capturer.Tribe);

        Phase = HolyStoneWarPhase.Cooldown;
        CooldownRemaining = testMode ? TestModeCooldown : NormalCooldown;
        PendingCandidateCharacterId = null;
        logger.LogInformation("HolyStoneWar: cycle reset -- cooldown armed for {Cooldown}", CooldownRemaining);
    }

    /// <summary>
    ///     Contract step 7's tribe-wide grant: every OTHER online, non-dead character sharing the winning tribe
    ///     (exact tribe match, NOT ally-inclusive -- distinct from the contest-phase holder check) within
    ///     <see cref="HolyStoneWarSite.ParticipationRadius" /> of the Stone, gated on having reborn at least
    ///     once. Scoped to the Stone's own map only, matching "within a ... radius of the Stone."
    /// </summary>
    private void GrantTribeWideParticipationRewards(Zone zone, PlayerRuntimeState capturer)
    {
        var participationRadiusSq = site.ParticipationRadius * site.ParticipationRadius;

        foreach (var player in zone.Players)
        {
            if (player.CharacterId == capturer.CharacterId)
                continue;
            if (player.Tribe != capturer.Tribe)
                continue;
            if (player.IsDead)
                continue;
            if (player.RebirthCount < 1)
                continue; // never reborn -- receives nothing from this participation grant
            if (DistanceSquared(player.PosX, player.PosZ, site.StoneX, site.StoneZ) > participationRadiusSq)
                continue;

            rewardGateway.GrantParticipationReward(player);
        }
    }

    /// <summary>
    ///     Contract step 8's quest-progress advance: every online, non-dead character of the winning tribe,
    ///     server-wide (no distance/reward-eligibility gate -- can fire for tribemates nowhere near the Stone),
    ///     matching the contract's explicit "this check has no distance ... gate" statement.
    /// </summary>
    private void AdvanceQuestProgressServerWide(byte winningTribeId)
    {
        foreach (var otherZone in zones.Zones)
        foreach (var player in otherZone.Players)
        {
            if (player.Tribe != winningTribeId)
                continue;
            if (player.IsDead)
                continue;

            rewardGateway.AdvanceQuestProgress(player);
        }
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }
}
