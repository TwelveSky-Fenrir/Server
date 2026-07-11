namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy <c>aRankPoint</c> -- the HSB (Holy Stone Battle) rank-point accumulator the code-40
    ///     cluster-wide reset (<see cref="Zone.ApplyHolyStoneBattleRankReset" />) clears to zero every time the
    ///     Tribe Symbol Battle window opens. Only ever awarded by the <c>USE_RANK_POINT</c>-gated accumulator
    ///     (holy-stone-strike / cross-tribe-kill grants, the 3000 cap, the code-671 "reached max rank"
    ///     announcement) -- and that macro is commented out at its sole declaration and defined nowhere under
    ///     <c>Server/</c>, so this value is always 0 in the shipped build. Modeled anyway (not deleted) because
    ///     the code-40 reset that clears it IS live, and a future contract may resurrect the accumulator as a
    ///     deliberate Fenrir product addition.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/Protocol/STRUCT.h:489 (the in-memory avatar copy's own <c>aRankPoint</c>
    ///     field) ; :776 (the persistent store's separate, second <c>aRankPoint</c> field, kept in sync by the
    ///     code-40 reset's step 3 -- Server/ts25zone/S07_MyGame08.cpp:263). Fenrir has no DB column for either
    ///     store yet (no schema exists for a field that can never hold a nonzero value in production), so this
    ///     single property stands in for BOTH legacy locations until a real accumulator lands; see the C15-hsb-
    ///     reset contract's own Open Questions and <see cref="Zone.ApplyHolyStoneBattleRankReset" />'s remarks.
    /// </remarks>
    public int RankPoint { get; set; }

    /// <summary>
    ///     Legacy <c>aRankPointDate</c> (YYYYMMDD, <see cref="Simulation.GameDate" /> encoding) -- stamped to
    ///     "today" by the code-40 cluster-wide reset (always, regardless of whether <see cref="RankPoint" /> or
    ///     the rank buff were actually cleared) and, separately, by the per-login HSB reset
    ///     (Server/ts25zone/S07_MyGame03.cpp:9044-9062, NOT implemented by this pass -- see that contract's own
    ///     Edge cases). 0 = never reset, which always compares unequal to a real <see cref="Simulation.GameDate.Today" />
    ///     value, matching the transferring-player branch's own "date not already today" gate on a
    ///     freshly-entered character.
    /// </summary>
    /// <remarks>Réf. C++ : Server/Header/Protocol/STRUCT.h:489-491 ; Server/ts25zone/S07_MyGame08.cpp:243-261.</remarks>
    public int RankPointDate { get; set; }
}
