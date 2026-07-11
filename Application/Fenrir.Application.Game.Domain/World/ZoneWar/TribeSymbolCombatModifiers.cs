using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe home for both outputs of <c>AdjustSymbolDamageInfo</c>: each tribe's own
///     damage-down malus for not currently holding its own battle symbol, AND (B15, wave15) its damage-up
///     bonus INCREMENT COUNT (0-4) from controlling other tribes' symbols/the neutral monster symbol/the
///     small-tribe-advantage fallback. Recomputed every tick by <see cref="TribeSymbolDamageModifierSystem" />;
///     read later by whichever combat-damage calculation wires it in
///     (<c>ServerDocs/12_ts25zone/10_MyGame02_Combat_ProcessAttack.md</c>) -- see
///     <see cref="Combat.MonsterCombatResolver.ResolvePvmAttack" />'s own remarks for how both halves are
///     applied to damage.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3144-3157 (function signature and the first per-tribe
///     calculation, directly verified) ; ServerDocs/12_ts25zone/08_MyGame01_PartieA.md §4 (states the full
///     function spans :3144-3338, repeating the same pattern once per tribe).
///     <para>
///         <b>RESOLVED this session (wave15 B15-pvm-tribesymbol-producer contract):</b> the previous blocker on
///         the damage-up bonus -- not knowing WHICH tribe currently holds another tribe's symbol slot -- is
///         closed by <see cref="WorldStateService.GetTribeSymbolOwner" /> (added this session; see its own
///         remarks). <see cref="GetDamageUpBonusIncrementCount" /> below is the fully-grounded increment COUNT
///         (0-4: the own-slot gate, up to three other-slot increments, one monster-symbol increment, OR the
///         one-increment small-tribe-advantage fallback when the first two produced nothing) -- every gate,
///         threshold, and cap here is a literal number the B15 contract's own prose states (the four-increment
///         ceiling, the ten-point small-tribe floor), not an invented one.
///     </para>
///     <para>
///         <b>RESOLVED this session (<c>tribesymbol-damage-magnitude</c> contract):</b> the actual FLAT DAMAGE
///         magnitude each increment contributes is now grounded, not guessed --
///         <c>Server/ts25zone/S07_MyGame02.cpp:2314-2318</c> collapses to a flat
///         <see cref="Combat.MonsterCombatResolver.DamageUpBonusFlatPerIncrement" /> (500) per increment, once
///         <c>tTribeSymbolDamageUp</c>'s own 0.1-per-increment build-up
///         (<c>S07_MyGame01.cpp:3155,3160,3165,3170,3178,3186</c>, repeated per tribe across <c>:3144-3338</c>)
///         is substituted into the formula. <see cref="GetDamageUpBonusIncrementCount" />'s result is now
///         multiplied by that flat amount inside <see cref="Combat.MonsterCombatResolver.ResolvePvmAttack" />,
///         applied immediately after the elemental-damage term and before the damage-down malus above (which
///         compounds on top of it) -- see that method's own remarks for the exact insertion point.
///     </para>
/// </remarks>
public sealed class TribeSymbolCombatModifiers
{
    /// <summary>Server/ts25zone/S07_MyGame01.cpp:3144-3157: the flat penalty for not holding one's own symbol.</summary>
    public const float OwnSymbolLostDamageDownPenalty = 0.2f;

    /// <summary>
    ///     B15 contract, Side effects §1: "the maximum attainable total across (c)+(d) is four increments (own
    ///     stone required, plus all three others, plus the monster symbol)". The small-tribe fallback in (e)
    ///     never adds on top of this -- it is only reachable when (c)+(d) produced zero, so the count this class
    ///     exposes never exceeds this ceiling either way.
    /// </summary>
    public const int MaxDamageUpBonusIncrementCount = 4;

    /// <summary>
    ///     B15 contract, Side effects §1e: the small-tribe-advantage fallback's own point floor -- "a tribe below
    ///     that ten-point floor is never eligible for the fallback regardless of how low its total is".
    /// </summary>
    public const int SmallTribeAdvantagePointFloor = 10;

    private readonly int[] _damageUpBonusIncrementCount = new int[WorldStateService.TribeCount];
    private readonly float[] _damageDownPenalty = new float[WorldStateService.TribeCount];
    private readonly Lock _lock = new();

    /// <summary>
    ///     This tribe's current damage-down penalty -- either <see cref="OwnSymbolLostDamageDownPenalty" /> (it
    ///     does not currently hold its own symbol slot) or 0 (it does).
    /// </summary>
    public float GetDamageDownPenalty(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _damageDownPenalty[tribeId];
        }
    }

    internal void SetDamageDownPenalty(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _damageDownPenalty[tribeId] = value;
        }
    }

    /// <summary>
    ///     B15 (wave15) -- this tribe's current damage-up bonus INCREMENT COUNT, 0-<see cref="MaxDamageUpBonusIncrementCount" />.
    ///     A count, not a damage value -- see this class's own remarks for why the per-increment magnitude is
    ///     deliberately not modeled yet.
    /// </summary>
    public int GetDamageUpBonusIncrementCount(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _damageUpBonusIncrementCount[tribeId];
        }
    }

    internal void SetDamageUpBonusIncrementCount(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _damageUpBonusIncrementCount[tribeId] = value;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= WorldStateService.TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId,
                $"TribeId must be 0-{WorldStateService.TribeCount - 1}.");
    }
}
