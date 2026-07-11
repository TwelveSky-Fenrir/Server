namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     The four persistent per-avatar "pity" counters the loot-box (op23 box-open) families keep across opens
///     (workstream C10): each ticks up by one on every open of its box and, on reaching the box's ceiling,
///     forces that box's guaranteed reward and resets to zero. Every field here is mutated only from this
///     character's own zone tick thread -- the same single-writer contract every other
///     <see cref="PlayerRuntimeState" /> field relies on -- and the pure counter arithmetic lives in
///     <see cref="Consumables.LootBoxRewardResolver.PityStep" /> /
///     <see cref="Consumables.LootBoxRewardResolver.RollCloak" />,
///     never re-derived at a call site.
/// </summary>
/// <remarks>
///     Persistence posture (identical to the session-scoped sub-group documented on
///     <see cref="PlayerRuntimeState" />.Consumables.cs's protection-charge counters): the legacy stores these
///     as per-character fields (<c>gBox2249</c>/<c>gBox8111</c>/<c>gBox8114</c>/<c>gBox8115</c>), but no
///     game.Characters column, world-entry hydration, zone-transfer carry-through, or write-behind persist-back
///     is wired for them yet -- so each starts at its C# default (0) in production and does NOT yet survive a
///     relog or zone transfer. Closing that requires (a) a schema column + hydration path
///     (fenrir-database-engineer) and (b) a mirror field on <c>TribeProgressZoneCommand</c> plus its
///     <c>Zone.ApplyTribeProgressCommand</c> apply arm so the incremented counter reaches this state on the
///     tick thread the same way <see cref="LodRounds" />/the Eat*Potion counters already do -- both flagged in
///     this workstream's wiring manifest. As of workstream C10-cloakbox2249, box 2249 IS registered and
///     exercises <see cref="CloakLuckyBoxPity" /> in-memory via <c>LootBoxUseItemHandler</c>'s reward-id-override
///     hook -- the persistence gap above still applies to it (session-scoped only, reset to 0 on relog/zone
///     transfer). As of workstream C10-remaining-box-pools, 8111/8114/8115's own fallback pools were recovered
///     (<c>M15PetLuckyBox8111RewardTable</c>/<c>CloakVariantBox8114RewardTable</c>/<c>MountVariantBox8115RewardTable</c>)
///     and all three ARE now registered the same way box 2249 is -- the SAME session-scoped-only persistence
///     gap now applies to all four pity fields uniformly, not an unrelated additional gap.
/// </remarks>
public partial class PlayerRuntimeState
{
    /// <summary>
    ///     <c>gBox2249</c> -- the Cloak Lucky Box (world.Items 2249) pity counter. Ceiling 100: at 100 opens the
    ///     box forces reward 1401 and this resets to 0 (Server/ts25zone/S04_MyWork03.cpp:1105-1114). Consumed by
    ///     <see cref="Loot.CloakBoxRewardTable.Roll" /> (composes
    ///     <see cref="Consumables.LootBoxRewardResolver.PityStep" /> directly -- NOT
    ///     <see cref="Consumables.LootBoxRewardResolver.RollCloak" />, see that table's own remarks for why).
    /// </summary>
    public int CloakLuckyBoxPity { get; set; }

    /// <summary>
    ///     <c>gBox8111</c> -- the M15 Pet Lucky Box (world.Items 8111) pity counter. Ceiling 200: at 200 opens
    ///     the box forces reward 1016 or 1012 and this resets to 0 (Server/ts25zone/S04_MyWork03.cpp:1615-1621,
    ///     7729-7735). See <see cref="Consumables.LootBoxRewardResolver.PityStep" />.
    /// </summary>
    public int M15PetLuckyBoxPity { get; set; }

    /// <summary>
    ///     <c>gBox8114</c> -- the cloak-variant box (world.Items 8114) pity counter. Ceiling 200: at 200 opens
    ///     the box forces reward 1403 and this resets to 0 (Server/ts25zone/S04_MyWork03.cpp:7866-7872).
    /// </summary>
    public int CloakVariantBoxPity { get; set; }

    /// <summary>
    ///     <c>gBox8115</c> -- the mount-variant box (world.Items 8115) pity counter. Ceiling 200: at 200 opens
    ///     the box forces reward 1307 or 1315 and this resets to 0 (Server/ts25zone/S04_MyWork03.cpp:7923-7929).
    /// </summary>
    public int MountVariantBoxPity { get; set; }
}
