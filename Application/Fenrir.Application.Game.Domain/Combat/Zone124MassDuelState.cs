namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>The decided outcome of a concluded zone124 mass-duel (surviving-headcount comparison).</summary>
public enum Zone124DuelWinner : byte
{
    /// <summary>Equal surviving headcounts -- a draw.</summary>
    Draw,

    /// <summary>Team one has the larger surviving headcount.</summary>
    Team1,

    /// <summary>Team two has the larger surviving headcount.</summary>
    Team2
}

/// <summary>What a single qualifying-tick <see cref="Zone124MassDuelState.Advance" /> resolved to.</summary>
public enum Zone124CountdownAction : byte
{
    /// <summary>No event running -- nothing happened.</summary>
    Idle,

    /// <summary>Countdown decremented; no remaining-time broadcast is due this step.</summary>
    Decremented,

    /// <summary>Countdown decremented; a remaining-time broadcast is due this step (fixed cadence).</summary>
    DecrementedBroadcastTime,

    /// <summary>A team was already empty -- the event tore down early with no headcount winner.</summary>
    TornDownEmptyTeam,

    /// <summary>The final unit was reached -- the event concluded with <see cref="Zone124CountdownStep.Winner" />.</summary>
    Concluded
}

/// <summary>Result of one <see cref="Zone124MassDuelState.Advance" /> call.</summary>
/// <param name="Action">What the Zone must do this step.</param>
/// <param name="RemainingUnits">The countdown value after this step (0 on teardown/conclusion).</param>
/// <param name="Winner">
///     Meaningful only when <paramref name="Action" /> is <see cref="Zone124CountdownAction.Concluded" />
///     .
/// </param>
public readonly record struct Zone124CountdownStep(
    Zone124CountdownAction Action,
    int RemainingUnits,
    Zone124DuelWinner Winner);

/// <summary>
///     Zone-wide mass-duel countdown state for a map-124 process (legacy <c>mDuel_124_RemainTime</c> /
///     <c>mDuel_124_Pvp</c> / the two-element team-headcount array -- <c>H07_MyGame.h:238-242</c>;
///     <c>Process_Zone_124_TYPE</c>, <c>Server/ts25zone/S07_MyGame01.cpp:4351-4494</c>; start/end/out
///     sub-commands 601/602/603, <c>Server/ts25zone/S04_MyWork04.cpp:1879-2013</c>). One instance per map-124
///     <c>Zone</c>, mutated only on that Zone's tick thread (single writer). This type owns the countdown value,
///     the active flag, and the two team headcounts, plus the pure decrement / empty-teardown / winner decision
///     logic; it does NOT enroll players, relocate them, assign team/duel-pairing markers, or broadcast -- those
///     are the Zone's side effects, driven by the <see cref="Zone124CountdownStep" /> this returns. This is the
///     same countdown branch C reads via <see cref="Zone124DuelOverrideResolver" />. See branch C (and its
///     C-supporting section) of the B10 behavior contract.
/// </summary>
/// <remarks>
///     <b>Tick cadence is the caller's concern.</b> Legacy decrements the countdown only on every second tick
///     (<c>S07_MyGame01.cpp:4403,:4491</c>) and the real-time duration of one countdown unit rests on an
///     asserted ~499 ms tick period the source finding does NOT tie to a cited constant.
///     <see cref="Advance" /> therefore models exactly ONE qualifying (decrementing) step and leaves "which
///     ticks qualify" and "how long a unit lasts in wall-clock time" to the Zone tick loop / realtime-simulation
///     owner -- do not assume one unit == one second here. See the B10 open questions.
/// </remarks>
public sealed class Zone124MassDuelState
{
    /// <summary>The countdown value the event starts at (<c>S04_MyWork04.cpp:1904</c>).</summary>
    public const int StartUnits = 60;

    /// <summary>
    ///     A remaining-time broadcast is emitted whenever the post-decrement countdown is a multiple of this
    ///     ("five-unit cadence", <c>S07_MyGame01.cpp:4427-4429</c>). The exact modulo phase (post- vs.
    ///     pre-decrement) is not pinned by the citation -- see the B10 open questions.
    /// </summary>
    public const int TimeBroadcastCadenceUnits = 5;

    /// <summary>Whether a mass-duel event is currently running on this map-124 zone.</summary>
    public bool Active { get; private set; }

    /// <summary>Remaining countdown units (0 when no event is running). Read by <see cref="Zone124DuelOverrideResolver" />.</summary>
    public int RemainingUnits { get; private set; }

    /// <summary>Surviving headcount of team one.</summary>
    public int Team1Count { get; private set; }

    /// <summary>Surviving headcount of team two.</summary>
    public int Team2Count { get; private set; }

    /// <summary>
    ///     Event start (sub-command 601): seeds the countdown to <see cref="StartUnits" />, raises the active
    ///     flag, and seeds both team headcounts from the enrollment the caller has already performed. The
    ///     enrollment itself (the radius test around the two team centers, team/duel-pairing marker assignment,
    ///     relocation to staging points) is the Zone's responsibility -- this only records its resulting counts.
    /// </summary>
    public void Start(int team1Count, int team2Count)
    {
        Active = true;
        RemainingUnits = StartUnits;
        Team1Count = team1Count;
        Team2Count = team2Count;
    }

    /// <summary>
    ///     Event end / out (sub-commands 602 / 603): zeroes the countdown, both headcounts, and the active flag.
    ///     Releasing participants from duel state is the Zone's own side effect.
    /// </summary>
    public void Reset()
    {
        Active = false;
        RemainingUnits = 0;
        Team1Count = 0;
        Team2Count = 0;
    }

    /// <summary>
    ///     Records the current surviving headcounts (the Zone maintains these as participants die or leave; the
    ///     winner and the empty-team teardown are decided purely from them). No-op on the active flag / countdown.
    /// </summary>
    public void SetTeamCounts(int team1Count, int team2Count)
    {
        Team1Count = team1Count;
        Team2Count = team2Count;
    }

    /// <summary>
    ///     Advances the countdown by one qualifying tick and reports what the Zone must do. Order matches
    ///     <c>Process_Zone_124_TYPE</c>: inactive -&gt; nothing; either team empty -&gt; early teardown (no
    ///     winner, checked before the countdown so it wins even on the final unit); otherwise, at the final unit
    ///     -&gt; conclude with the headcount winner; otherwise decrement and report whether a remaining-time
    ///     broadcast is due. Concluding and empty-teardown both leave the state <see cref="Reset" />.
    /// </summary>
    public Zone124CountdownStep Advance()
    {
        if (!Active)
            return new Zone124CountdownStep(Zone124CountdownAction.Idle, 0, Zone124DuelWinner.Draw);

        // Early teardown -- checked before the countdown (:4361-4401), so it wins even on the final unit. The
        // result indicator for the empty case is NOT established by the citations (see B10 open questions), so
        // no winner is decided here.
        if (Team1Count == 0 || Team2Count == 0)
        {
            Reset();
            return new Zone124CountdownStep(Zone124CountdownAction.TornDownEmptyTeam, 0, Zone124DuelWinner.Draw);
        }

        // "When one unit remains" -- decide the winner by surviving headcount and tear down (:4441-4489).
        // Defensive <= 1 (legacy is exactly == 1; the active state can never hold 0 without a Reset).
        if (RemainingUnits <= 1)
        {
            var winner = DecideWinner();
            Reset();
            return new Zone124CountdownStep(Zone124CountdownAction.Concluded, 0, winner);
        }

        RemainingUnits--;
        var broadcastDue = RemainingUnits % TimeBroadcastCadenceUnits == 0;
        return new Zone124CountdownStep(
            broadcastDue ? Zone124CountdownAction.DecrementedBroadcastTime : Zone124CountdownAction.Decremented,
            RemainingUnits,
            Zone124DuelWinner.Draw);
    }

    /// <summary>Larger surviving headcount wins; equal is a draw (:4441-4489).</summary>
    public Zone124DuelWinner DecideWinner()
    {
        if (Team1Count == Team2Count)
            return Zone124DuelWinner.Draw;
        return Team1Count > Team2Count ? Zone124DuelWinner.Team1 : Zone124DuelWinner.Team2;
    }
}
