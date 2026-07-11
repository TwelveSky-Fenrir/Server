using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe home for both outputs of <c>AdjustSymbolDamageInfo</c>: each tribe's own
///     damage-down malus for not currently holding its own battle symbol, AND (B15, wave15) its damage-up
///     bonus INCREMENT COUNT (0-4) from controlling other tribes' symbols/the neutral monster symbol/the
///     small-tribe-advantage fallback. Recomputed every tick by <see cref="TribeSymbolDamageModifierSystem" />;
///     read later by whichever combat-damage calculation wires it in
///     (<c>ServerDocs/12_ts25zone/10_MyGame02_Combat_ProcessAttack.md</c>) -- see
///     <see cref="Combat.MonsterCombatResolver.ResolvePvmAttack" />'s own remarks for which half is actually
///     applied to damage today.
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
///         <b>STILL NOT modeled -- genuinely unrecoverable, not guessed:</b> the actual FLAT DAMAGE magnitude
///         each increment contributes at the point of application. The B15 contract's own Side-effects text
///         describes it only in non-numeric prose ("the very first increment already worth a meaningfully large
///         flat addition, roughly the size of the increment itself times several hundred, and each further
///         increment worth the same amount again") -- not a citation-backed number. Per this project's
///         never-invent-a-magnitude rule, <see cref="GetDamageUpBonusIncrementCount" />'s result is therefore
///         NOT yet multiplied into any damage calculation anywhere in this codebase; it is computed and
///         available for the day a fresh citation grounds the actual per-increment amount. Flag for
///         <c>cpp-security-debt-auditor</c>/<c>cpp-zone-gameplay-analyst</c> re-check if that magnitude is ever
///         needed.
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
