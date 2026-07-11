namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The side-effect seam for the Zone175 mission's pure state machine (<see cref="Zone175MissionCore" />).
///     Every touch of the world (summoning, monster queries, rewards, disconnects, broadcasts/logs) goes through
///     here so the state machine itself stays a pure, fully unit-testable function of (state, config, clock,
///     ticks). The production implementation is <see cref="ZoneZone175MissionEffects" /> (adapts to a live
///     <c>Zone</c>); tests use a recording fake.
/// </summary>
public interface IZone175MissionEffects
{
    /// <summary>
    ///     Whether any qualifying player is present (ready, not mid-zone-transition, not hiding). A dead-but-
    ///     present player counts as present -- see <see cref="Zone175EligibilityRules.IsPresent(bool,bool)" />.
    ///     Drives the empty-abort check.
    /// </summary>
    public bool AnyQualifyingPlayerPresent();

    /// <summary>
    ///     How many wave-boss monsters of <paramref name="stage" />'s special type (40-44) are still alive.
    ///     Zero triggers the wave clear.
    /// </summary>
    public int CountLivingWaveBosses(int stage);

    /// <summary>Summon <paramref name="stage" />'s wave boss (special type 40-44), without a time limit.</summary>
    public void SummonWaveBoss(int stage);

    /// <summary>Summon the fixed-cadence combat trickle monsters for <paramref name="stage" />.</summary>
    public void SummonTrickle(int stage);

    /// <summary>Remove every live mission monster from the zone (on wave clear or abort).</summary>
    public void RemoveMissionMonsters();

    /// <summary>
    ///     Run the wave-clear reward routine for <paramref name="stage" /> over every reward-eligible player
    ///     (present AND not dead): un-stun, experience, the fixed money table, drops, CP, boss-damage reset.
    /// </summary>
    public void RewardQualifyingPlayers(int stage);

    /// <summary>Force-disconnect every player currently in the zone (terminal-state kick).</summary>
    public void ForceDisconnectAll();

    /// <summary>
    ///     Emit a phase-transition notification (center-directed broadcast / lifecycle log in the legacy;
    ///     collapsed to a structured log line in Fenrir). <paramref name="wave" /> is the 1-based wave when
    ///     meaningful (0 otherwise); <paramref name="remaining" /> carries the pre-open countdown value for
    ///     <see cref="Zone175MissionEvent.PreOpenCountdown" /> (0 otherwise).
    /// </summary>
    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining);
}

/// <summary>
///     A total no-op effects sink -- every query answers "nobody present / nothing alive" and every command does
///     nothing. Lets <see cref="Zone175MissionCore" /> be exercised in isolation and gives
///     <see cref="Zone175LabyrinthConfig.Disabled" />-configured zones a safe default.
/// </summary>
public sealed class NullZone175MissionEffects : IZone175MissionEffects
{
    public static readonly NullZone175MissionEffects Instance = new();

    public bool AnyQualifyingPlayerPresent()
    {
        return false;
    }

    public int CountLivingWaveBosses(int stage)
    {
        return 0;
    }

    public void SummonWaveBoss(int stage)
    {
    }

    public void SummonTrickle(int stage)
    {
    }

    public void RemoveMissionMonsters()
    {
    }

    public void RewardQualifyingPlayers(int stage)
    {
    }

    public void ForceDisconnectAll()
    {
    }

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining)
    {
    }
}
