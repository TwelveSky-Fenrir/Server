namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Pure per-tribe totals formula for the level/rebirth-based tribe-point recompute: a flat 1000 baseline for
///     every tribe, plus -- for every persisted character belonging to that tribe whose primary level is at
///     least <see cref="LevelThreshold" /> -- (primary level minus <see cref="LevelOffset" />) plus (secondary
///     level times <see cref="SecondaryLevelMultiplier" />) plus (rebirth count times
///     <see cref="RebirthMultiplier" />), and finally a flat <see cref="BonusTribeBonus" /> added to
///     <see cref="BonusTribeId" /> alone. Always a full recompute over the whole roster -- never an incremental
///     delta -- and always produces all 4 totals together, even when the roster is empty (every tribe still
///     gets at least the baseline).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S08_MyDB.cpp:107-136 (<c>GetTribePoint(int tTribePoint[MAX_TRIBE_NUM])</c>,
///     <c>#ifdef OLD_TRIBE_POINT</c>) -- the 1000 baseline, level-145 gate, the three-term sum, and tribe 3's
///     +800 bonus (line 131) ; Server/Header/Protocol/DEFINE.h:93 (<c>#define OLD_TRIBE_POINT</c>, unconditional)
///     -- confirms this formula compiles in every build variant ; Server/Header/Protocol/DEFINE.h:24,29
///     (<c>#define __GOD__</c> in both branches) -- confirms the compiling table-index bug this class's formula
///     is deliberately NOT responsible for (see <see cref="ITribePointRosterGateway" />'s own remarks) ;
///     Server/Header/Protocol/DEFINE.h:5 (<c>//#define SEA_SERVER</c>, commented out) -- confirms the
///     <c>#ifndef SEA_SERVER</c> guard around tribe 3's bonus is active ; Server/Header/Protocol/DEFINE.h:309
///     (<c>#define MAX_TRIBE_NUM 4</c>) -- the fixed 4-tribe domain, see <see cref="WorldStateService.TribeCount" />.
/// </remarks>
public static class TribePointLevelRecompute
{
    /// <summary>Flat baseline every tribe receives regardless of roster composition.</summary>
    public const int Baseline = 1000;

    /// <summary>A character contributes nothing to its tribe's total below this primary level.</summary>
    public const int LevelThreshold = 145;

    /// <summary>Subtracted from a qualifying character's own primary level before summing.</summary>
    public const int LevelOffset = 112;

    public const int SecondaryLevelMultiplier = 3;
    public const int RebirthMultiplier = 3;

    /// <summary>The one tribe that receives <see cref="BonusTribeBonus" /> on top of the shared formula.</summary>
    public const byte BonusTribeId = 3;

    public const int BonusTribeBonus = 800;

    /// <summary>
    ///     Computes all 4 tribes' totals from a fresh roster snapshot. Roster rows for a tribe id outside 0-3 are
    ///     skipped defensively (never observed in a well-formed roster, but this keeps the formula total, not
    ///     partial, for any malformed input).
    /// </summary>
    public static int[] ComputeTotals(IReadOnlyList<TribeRosterCharacterSnapshot> roster)
    {
        var totals = new int[WorldStateService.TribeCount];
        Array.Fill(totals, Baseline);

        foreach (var character in roster)
        {
            if (character.TribeId >= WorldStateService.TribeCount)
                continue;
            if (character.Level1 < LevelThreshold)
                continue;

            totals[character.TribeId] += character.Level1 - LevelOffset
                                          + character.Level2 * SecondaryLevelMultiplier
                                          + character.RebirthCount * RebirthMultiplier;
        }

        totals[BonusTribeId] += BonusTribeBonus;
        return totals;
    }
}
