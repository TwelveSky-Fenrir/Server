using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe home for one output of <c>AdjustSymbolDamageInfo</c>: each tribe's own
///     damage-down penalty for not currently holding its own battle symbol. Recomputed every tick by
///     <see cref="TribeSymbolDamageModifierSystem" />; read later by whichever combat-damage calculation wires
///     it in (<c>ServerDocs/12_ts25zone/10_MyGame02_Combat_ProcessAttack.md</c>, out of scope for this class --
///     see its own remarks for why only this one half of the legacy function is modeled here).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3144-3157 (function signature and the first per-tribe
///     calculation, directly verified) ; ServerDocs/12_ts25zone/08_MyGame01_PartieA.md §4 (states the full
///     function spans :3144-3338, repeating the same pattern once per tribe).
///     <para>
///         GAP -- deliberately NOT modeled here, not guessed: (1) the per-tribe DAMAGE-UP bonus (0.1 per other
///         tribe's symbol slot held directly or via alliance, up to 0.3 combined) and its companion CP-bonus
///         counter both require knowing WHICH tribe currently holds another tribe's symbol slot -- the source
///         schema and <see cref="WorldStateService" /> only record, per slot, whether that slot's OWN tribe
///         still holds it (<see cref="WorldStateService.GetTribe" />.<c>HasSymbol</c>), never the identity of
///         whichever OTHER tribe captured it when that bool is false (the exact same "no current-holder
///         identity" limitation <see cref="TribeBankTaxAccumulator" />'s own remarks already document for the
///         unrelated tribe-symbol tax-indirection seam). Computing the damage-up bonus without that identity
///         would mean guessing which tribe benefits from a lost slot -- not implemented here pending either a
///         schema change (a <c>fenrir-database-engineer</c> concern, out of this class's scope) or a fresh
///         legacy-behavior-translator finding confirming an alternative derivation; (2) the "monster symbol"
///         and "small-tribe-advantage" fallback bonuses (an additional 0.1 each) were not independently
///         re-derived by the translated contract past the first symbol-slot calculation, so they are likewise
///         not modeled here.
///     </para>
/// </remarks>
public sealed class TribeSymbolCombatModifiers
{
    /// <summary>Server/ts25zone/S07_MyGame01.cpp:3144-3157: the flat penalty for not holding one's own symbol.</summary>
    public const float OwnSymbolLostDamageDownPenalty = 0.2f;

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

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= WorldStateService.TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId,
                $"TribeId must be 0-{WorldStateService.TribeCount - 1}.");
    }
}
