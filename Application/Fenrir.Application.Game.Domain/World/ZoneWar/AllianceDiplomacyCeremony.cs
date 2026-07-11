using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum AllianceCeremonyPhase
{
    Idle,
    RejectionMessage,
    NewAllianceNegotiation,
    AlreadyAlliedNegotiation,
    PostNegotiationCooldown
}

public enum AllianceCeremonyNotice
{

        None,

        Rejected,

        NewAllianceProgress,

        NewAllianceAborted,

        AlreadyAlliedProgress,

        AlreadyAlliedAborted
}

public readonly record struct AllianceCeremonyCandidate(int CharacterId, byte TribeId);

public readonly record struct AllianceCeremonyTickResult(
    AllianceCeremonyNotice Notice,
    AllianceCeremonyCandidate? RecipientOne,
    AllianceCeremonyCandidate? RecipientTwo,
    int RemainingCountdown)
{
    public static readonly AllianceCeremonyTickResult None = new(AllianceCeremonyNotice.None, null, null, 0);
}

public sealed class AllianceDiplomacyCeremony
{

        public const int MinimumAllyAdjustedPoints = 100;

        public const int RejectionMessageDurationRawTicks = 120;

        public const int PostNegotiationCooldownDurationRawTicks = 7200;

        public const int NegotiationConfirmationDurationRawTicks = 60;

        public const int ReAllianceCooldownDays = 14;

    private readonly int _alreadyAlliedNegotiationDurationRawTicks;
    private readonly ZoneEventBroadcaster _broadcaster;

    private readonly AllianceCooldownTracker _cooldowns;
    private readonly Lock _lock = new();
    private readonly ILogger<AllianceDiplomacyCeremony> _logger;
    private readonly int _newAllianceNegotiationDurationRawTicks;
    private readonly WorldStateService _worldState;

    private AllianceCeremonyCandidate? _leaderOne;
    private AllianceCeremonyCandidate? _leaderTwo;
    private AllianceCeremonyPhase _phase = AllianceCeremonyPhase.Idle;
    private int _phaseEnteredAtRawTick;
    private int _rawTick;
    private int _remainingCountdown;

        public AllianceDiplomacyCeremony(
        WorldStateService worldState,
        AllianceCooldownTracker cooldowns,
        ZoneEventBroadcaster broadcaster,
        ILogger<AllianceDiplomacyCeremony> logger,
        int newAllianceNegotiationDurationRawTicks,
        int alreadyAlliedNegotiationDurationRawTicks)
    {
        if (newAllianceNegotiationDurationRawTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(newAllianceNegotiationDurationRawTicks),
                newAllianceNegotiationDurationRawTicks, "Must be positive.");
        if (alreadyAlliedNegotiationDurationRawTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(alreadyAlliedNegotiationDurationRawTicks),
                alreadyAlliedNegotiationDurationRawTicks, "Must be positive.");

        _worldState = worldState;
        _cooldowns = cooldowns;
        _broadcaster = broadcaster;
        _logger = logger;
        _newAllianceNegotiationDurationRawTicks = newAllianceNegotiationDurationRawTicks;
        _alreadyAlliedNegotiationDurationRawTicks = alreadyAlliedNegotiationDurationRawTicks;
    }

    public AllianceCeremonyPhase Phase
    {
        get
        {
            lock (_lock)
            {
                return _phase;
            }
        }
    }

        public AllianceCeremonyTickResult Tick(AllianceCeremonyCandidate? postOneLeader,
        AllianceCeremonyCandidate? postTwoLeader, DateOnly today)
    {
        lock (_lock)
        {
            _rawTick++;

            return _phase switch
            {
                AllianceCeremonyPhase.Idle => EvaluateIdle(postOneLeader, postTwoLeader, today),
                AllianceCeremonyPhase.RejectionMessage => AdvanceRejection(),
                AllianceCeremonyPhase.NewAllianceNegotiation => AdvanceNegotiation(postOneLeader, postTwoLeader,
                    false, today),
                AllianceCeremonyPhase.AlreadyAlliedNegotiation => AdvanceNegotiation(postOneLeader, postTwoLeader,
                    true, today),
                AllianceCeremonyPhase.PostNegotiationCooldown => AdvanceCooldown(),
                _ => AllianceCeremonyTickResult.None
            };
        }
    }

    private AllianceCeremonyTickResult EvaluateIdle(AllianceCeremonyCandidate? postOneLeader,
        AllianceCeremonyCandidate? postTwoLeader, DateOnly today)
    {
        if (postOneLeader is not { } one || postTwoLeader is not { } two)
            return AllianceCeremonyTickResult.None;

        var isAlreadyAllied = _worldState.GetAllyOf(one.TribeId) == two.TribeId;

        if (isAlreadyAllied)
        {
            EnterNegotiation(AllianceCeremonyPhase.AlreadyAlliedNegotiation, one, two,
                _alreadyAlliedNegotiationDurationRawTicks);
            return AllianceCeremonyTickResult.None;
        }

        if (IsDisqualified(one.TribeId, two.TribeId, today))
        {
            EnterRejection();
            _logger.LogInformation(
                "Alliance ceremony rejected pairing of tribe {TribeA} and tribe {TribeB}", one.TribeId, two.TribeId);
            return new AllianceCeremonyTickResult(AllianceCeremonyNotice.Rejected, one, two, 0);
        }

        EnterNegotiation(AllianceCeremonyPhase.NewAllianceNegotiation, one, two,
            _newAllianceNegotiationDurationRawTicks);
        return AllianceCeremonyTickResult.None;
    }

    private AllianceCeremonyTickResult AdvanceRejection()
    {
        if (_rawTick - _phaseEnteredAtRawTick >= RejectionMessageDurationRawTicks)
            TransitionToIdle();

        return AllianceCeremonyTickResult.None;
    }

    private AllianceCeremonyTickResult AdvanceCooldown()
    {
        if (_rawTick - _phaseEnteredAtRawTick >= PostNegotiationCooldownDurationRawTicks)
            TransitionToIdle();

        return AllianceCeremonyTickResult.None;
    }

    private AllianceCeremonyTickResult AdvanceNegotiation(AllianceCeremonyCandidate? postOneLeader,
        AllianceCeremonyCandidate? postTwoLeader, bool isAlreadyAllied, DateOnly today)
    {
        if (_rawTick % 2 != 0)
            return AllianceCeremonyTickResult.None;

        var leaderOne = _leaderOne!.Value;
        var leaderTwo = _leaderTwo!.Value;

        var stillValid = postOneLeader is { } one && postTwoLeader is { } two &&
                         one == leaderOne && two == leaderTwo;

        if (!stillValid)
        {
            TransitionToIdle();
            return new AllianceCeremonyTickResult(
                isAlreadyAllied
                    ? AllianceCeremonyNotice.AlreadyAlliedAborted
                    : AllianceCeremonyNotice.NewAllianceAborted,
                leaderOne, leaderTwo, 0);
        }

        _remainingCountdown--;

        if (_remainingCountdown > 0)
            return new AllianceCeremonyTickResult(
                isAlreadyAllied
                    ? AllianceCeremonyNotice.AlreadyAlliedProgress
                    : AllianceCeremonyNotice.NewAllianceProgress,
                leaderOne, leaderTwo, _remainingCountdown);

        if (isAlreadyAllied)
        {
            var eligibleAgain = today.AddDays(ReAllianceCooldownDays);
            _cooldowns.SetCooldownUntil(leaderOne.TribeId, eligibleAgain);
            _cooldowns.SetCooldownUntil(leaderTwo.TribeId, eligibleAgain);
            _broadcaster.AnnounceAllianceDissolved(leaderOne.TribeId, leaderTwo.TribeId);

            _logger.LogInformation(
                "Alliance ceremony dissolved the alliance between tribe {TribeA} and tribe {TribeB}, both under cooldown until {EligibleAgain:d}",
                leaderOne.TribeId, leaderTwo.TribeId, eligibleAgain);
        }


        EnterPostNegotiationCooldown();
        return AllianceCeremonyTickResult.None;
    }

    private void EnterNegotiation(AllianceCeremonyPhase phase, AllianceCeremonyCandidate one,
        AllianceCeremonyCandidate two, int durationRawTicks)
    {
        _phase = phase;
        _phaseEnteredAtRawTick = _rawTick;
        _leaderOne = one;
        _leaderTwo = two;
        _remainingCountdown = durationRawTicks;
    }

    private void EnterRejection()
    {
        _phase = AllianceCeremonyPhase.RejectionMessage;
        _phaseEnteredAtRawTick = _rawTick;
        _leaderOne = null;
        _leaderTwo = null;
    }

    private void EnterPostNegotiationCooldown()
    {
        _phase = AllianceCeremonyPhase.PostNegotiationCooldown;
        _phaseEnteredAtRawTick = _rawTick;
        _leaderOne = null;
        _leaderTwo = null;
    }

    private void TransitionToIdle()
    {
        _phase = AllianceCeremonyPhase.Idle;
        _leaderOne = null;
        _leaderTwo = null;
    }

        private bool IsDisqualified(byte tribeA, byte tribeB, DateOnly today)
    {
        var highestTribe = GetHighestAllyAdjustedPointsTribe();
        if (highestTribe == tribeA || highestTribe == tribeB)
            return true;

        if (_worldState.GetAllyOf(tribeA) is not null || _worldState.GetAllyOf(tribeB) is not null)
            return true;

        if (GetAllyAdjustedPoints(tribeA) < MinimumAllyAdjustedPoints ||
            GetAllyAdjustedPoints(tribeB) < MinimumAllyAdjustedPoints)
            return true;

        return _cooldowns.IsInCooldown(tribeA, today) || _cooldowns.IsInCooldown(tribeB, today);
    }

        private int GetAllyAdjustedPoints(byte tribeId)
    {
        var points = _worldState.GetTribe(tribeId).Points;
        if (_worldState.GetAllyOf(tribeId) is { } allyTribeId)
            points += _worldState.GetTribe(allyTribeId).Points;

        return points;
    }

        private byte? GetHighestAllyAdjustedPointsTribe()
    {
        Span<int> points = stackalloc int[WorldStateService.TribeCount];
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            points[tribeId] = GetAllyAdjustedPoints(tribeId);

        var max = int.MinValue;
        foreach (var value in points)
            if (value > max)
                max = value;

        byte? highestTribe = null;
        var tiedForMax = 0;
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            if (points[tribeId] == max)
            {
                tiedForMax++;
                highestTribe = tribeId;
            }

        return tiedForMax == 1 ? highestTribe : null;
    }
}
