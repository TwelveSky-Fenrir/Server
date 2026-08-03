using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum HolyStoneWarPhase : byte
{
    Cooldown,

    OpeningCountdown,

    Contest,

    ChallengePending
}

public sealed record HolyStoneWarSite(
    short MapId,
    float StoneX,
    float StoneY,
    float StoneZ,
    float CaptureRadius,
    float ParticipationRadius);

public sealed class HolyStoneWarCycle(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ZoneRegistry zones,
    IHolyStoneCaptureRewardGateway rewardGateway,
    ILogger<HolyStoneWarCycle> logger,
    HolyStoneWarSite site,
    IRandomSource? random = null,
    bool testMode = false,
    ZoneCenterBroadcastIngestor? siegeIngestor = null)
{
    public const int OpeningCountdownMinutes = 10;

    public const int ChallengeCountdownMinutes = 5;

    public static readonly TimeSpan NormalCooldown = TimeSpan.FromHours(3);

    public static readonly TimeSpan TestModeCooldown = TimeSpan.FromHours(1);

    private readonly MinuteCountdown _minuteCountdown = new();
    private readonly IRandomSource _random = random ?? SystemRandomSource.Instance;

    public HolyStoneWarPhase Phase { get; private set; } = HolyStoneWarPhase.Cooldown;

    public TimeSpan CooldownRemaining { get; private set; } = testMode ? TestModeCooldown : NormalCooldown;

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
                logger.LogInformation("HolyStoneWar: opens in {MinutesRemaining} minute(s)", remaining);
                broadcaster.AnnounceHolyStoneOpeningCountdown(remaining);
                continue;
            }

            if (minute != OpeningCountdownMinutes + 1)
                continue;

            logger.LogInformation("HolyStoneWar: war zone now open -- contest phase begins");
            broadcaster.AnnounceHolyStoneWarZoneOpen();
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
            if (player.IsMovingZone || player.VisibleState == 0)
                continue;
            if (player.IsDead)
                continue;
            if (HolyStoneTribeMatch.Matches(player.Tribe, holderTribe, allyOfHolder))
                continue;
            if (DistanceSquared(player, site) > captureRadiusSq)
                continue;

            PendingCandidateCharacterId = player.CharacterId;
            Phase = HolyStoneWarPhase.ChallengePending;
            _minuteCountdown.Reset();
            logger.LogInformation(
                "HolyStoneWar: challenger {CharacterId} (tribe {Tribe}) approaches -- {Minutes}-minute countdown begins",
                player.CharacterId, player.Tribe, ChallengeCountdownMinutes);
            broadcaster.AnnounceHolyStoneChallengerApproaching(player.Tribe, player.Name);
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
            candidate.IsMovingZone || candidate.VisibleState == 0 ||
            DistanceSquared(candidate, site) > captureRadiusSq)
        {
            CancelChallenge();
            return;
        }

        var wholeMinutes = _minuteCountdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
        {
            var minute = _minuteCountdown.MinutesElapsed;

            if (minute is >= 1 and <= ChallengeCountdownMinutes)
            {
                broadcaster.AnnounceHolyStoneChallengeCountdown(ChallengeCountdownMinutes + 1 - minute);
                continue;
            }

            if (minute != ChallengeCountdownMinutes + 1)
                continue;

            ResolveCapture(zone, candidate);
            return;
        }
    }

    private void CancelChallenge()
    {
        logger.LogInformation("HolyStoneWar: challenger {CharacterId} left/failed -- resuming scan",
            PendingCandidateCharacterId);

        broadcaster.AnnounceHolyStoneChallengeCancelled();

        PendingCandidateCharacterId = null;
        Phase = HolyStoneWarPhase.Contest;
    }

    private void ResolveCapture(Zone zone, PlayerRuntimeState capturer)
    {
        var previousHolder = worldState.World.Zone038WinTribe;
        if (previousHolder is { } previous)
        {
            EmitDtmValue(previous, 0);
            logger.LogInformation(
                "HolyStoneWar: cleared Zone038-DTM value for previous holder tribe {PreviousHolderTribe}",
                previous);
        }

        logger.LogInformation(
            "HolyStoneWar: new holder tribe {WinningTribe}, captured by {CharacterName} ({CharacterId})",
            capturer.Tribe, capturer.Name, capturer.CharacterId);
        broadcaster.AnnounceZone038Winner(capturer.Tribe, capturer.Name);

        var bonus = _random.NextInt32(3) + 1;
        EmitDtmValue(capturer.Tribe, bonus);
        logger.LogInformation(
            "HolyStoneWar: new holder tribe {WinningTribe} Zone038-DTM bonus set to {Bonus}",
            capturer.Tribe, bonus);

        rewardGateway.GrantCaptureReward(capturer);
        GrantTribeWideParticipationRewards(zone, capturer);
        PostZone038OccupationCredits(zone, capturer.Tribe);

        Phase = HolyStoneWarPhase.Cooldown;
        CooldownRemaining = testMode ? TestModeCooldown : NormalCooldown;
        PendingCandidateCharacterId = null;
        logger.LogInformation("HolyStoneWar: cycle reset -- cooldown armed for {Cooldown}", CooldownRemaining);
    }

    private void GrantTribeWideParticipationRewards(Zone zone, PlayerRuntimeState capturer)
    {
        var participationRadiusSq = site.ParticipationRadius * site.ParticipationRadius;

        foreach (var player in zone.Players)
        {
            if (player.CharacterId == capturer.CharacterId)
                continue;
            if (player.Tribe != capturer.Tribe)
                continue;
            if (player.IsMovingZone)
                continue;
            if (player.IsDead)
                continue;
            if (DistanceSquared(player, site) > participationRadiusSq)
                continue;

            rewardGateway.GrantParticipationReward(player);
        }
    }

    private void PostZone038OccupationCredits(Zone zone, byte winningTribe)
    {
        foreach (var player in zone.Players)
        {
            if (player.Tribe != winningTribe || player.IsDead || player.IsMovingZone)
                continue;

            if (!zone.Post(ZoneCommand.CreditZone038Occupation(player.CharacterId, winningTribe)))
                logger.LogWarning(
                    "Zone {MapId} inbox full: dropped Zone038 occupation credit for character {CharacterId}",
                    zone.MapId, player.CharacterId);
        }
    }

    private void EmitDtmValue(byte tribeId, int value)
    {
        if (siegeIngestor is null)
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload[..4], tribeId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..8], value);
        siegeIngestor.Ingest(ZoneCenterBroadcastIngestor.DtmEventCode, payload);
    }

    private static float DistanceSquared(PlayerRuntimeState player, HolyStoneWarSite site)
    {
        var dx = player.PosX - site.StoneX;
        var dy = player.PosY - site.StoneY;
        var dz = player.PosZ - site.StoneZ;
        return dx * dx + dy * dy + dz * dz;
    }
}
