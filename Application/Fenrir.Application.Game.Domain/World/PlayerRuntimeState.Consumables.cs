namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Banked counters for the <c>CZ_USE_INVENTORY_ITEM_SEND</c> (op 23) consumable families added on top of
///     the pre-existing Bottle/Guild-Scroll/Faction-Transfer/GP-ticket dispatch -- loot-box LOD round counter,
///     protection/enhancement charges, the two unambiguous banked cash-timer ids (Faction Notice, Taiyan
///     Key), and the five stat/elixir-potion counters. Most fields here are session-scoped only, same open
///     issue as <see cref="ProtectForHalo" />/<see cref="TribeNotifyScrollCount" />/<see cref="UseOrnament" />
///     above: some of these (e.g. the protection-charm counters) have no persisted game.Characters column at
///     all yet -- durability for that remaining sub-group is still a follow-up for fenrir-database-engineer.
///     The five <c>Eat*Potion</c> fields and <see cref="DropItemTime" /> are the exceptions: they are fully
///     wired end-to-end (world-entry hydration, zone-transfer carry-through, write-behind persist-back) -- see
///     each field's own remarks.
/// </summary>
public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Item 1434's banked "Life or Death" round counter -- the loot-box behavior contract's always-on
    ///     direct handler, gated on level cap + rebirth count, ceiling-checked at 2,000,000,000.
    /// </summary>
    public int LodRounds { get; set; }

    /// <summary>Preserve Charm (world.Items 593/1218) -- a refine-protection charge counter.</summary>
    public int ProtectForRefine { get; set; }

    /// <summary>
    ///     Protection Charm (world.Items 1103/1358/1455/8418) -- a destroy-protection charge counter. Distinct
    ///     from <see cref="ProtectForDestroy2" /> (Absolute Craft Ticket's own, separate counter).
    /// </summary>
    public int ProtectForDestroy { get; set; }

    /// <summary>Guardian Charm (world.Items 8103/8436) -- a costume-protection charge counter.</summary>
    public int ProtectForCostume { get; set; }

    /// <summary>Absolute Craft Ticket (world.Items 828/837) -- a second, distinct destroy-protection counter.</summary>
    public int ProtectForDestroy2 { get; set; }

    /// <summary>Lucky Enchant Scroll (world.Item 1126) -- an "improve item" charge counter.</summary>
    public int ImproveItemValue { get; set; }

    /// <summary>Lucky Combine/"Harmony" Scroll (world.Items 1146-1148/1231) -- an "add item" charge counter.</summary>
    public int AddItemValue { get; set; }

    /// <summary>Lucky Upgrade Scroll (world.Items 1149-1151/1232) -- a "high item" charge counter.</summary>
    public int HighItemValue { get; set; }

    /// <summary>
    ///     Lucky Drop/"Acquisition" Scroll (world.Items 1152-1154/1233) -- a "drop item time" (minutes) counter.
    ///     game.Characters.DropItemTime is the persisted column for this exact field, read back at world entry
    ///     (<c>CharacterWorldSnapshotDto.DropItemTime</c> / <c>PlayerEnterData.DropItemTime</c> /
    ///     <c>EnterWorldService</c>), carried through an in-process zone transfer
    ///     (<see cref="ZoneTransfer.CreateEnterData" />), and flushed back durably by the write-behind
    ///     progress batch (<c>CharacterProgressTvp</c>/<c>usp_Character_PersistProgressBatch</c>, via
    ///     <c>ProgressWriteBehindHost.FlushAsync</c>) -- the same "fully wired end-to-end" posture as the five
    ///     <c>Eat*Potion</c> counters above.
    /// </summary>
    public int DropItemTime { get; set; }

    /// <summary>Taiyan Key (world.Item 1049) -- aZone125Time, a dungeon-access timer gated on the level cap.</summary>
    public int TaiyanKeyTimer { get; set; }

    /// <summary>
    ///     aEatLifePotion -- lifetime count of Life/HP stat-potion "headroom" consumed (ordinary tier capped at
    ///     200, g12 tier at 400; see <see cref="Consumables.StatPotionResolver" />). Unlike every other field in
    ///     this file, <c>game.Characters.EatLifePotion</c> already exists as a persisted column (0-400
    ///     CHECK-constrained) and is already read back by <c>CharacterWorldSnapshotDto</c>/
    ///     <c>usp_Character_GetForWorldEntry</c> -- this field is the missing runtime-hydration link, not a new
    ///     schema gap. See <c>PlayerEnterData.EatLifePotion</c>/<c>EnterWorldService</c>/<c>ZoneTransfer</c> for
    ///     the world-entry/zone-transfer hydration and <c>Zone.ApplyTribeProgressCommand</c> for the
    ///     write-behind persist-back path (game.tvp_CharacterProgress/usp_Character_PersistProgressBatch).
    /// </summary>
    public int EatLifePotion { get; set; }

    /// <summary>aEatManaPotion -- same posture as <see cref="EatLifePotion" />, for the Mana/MP stat-potion family.</summary>
    public int EatManaPotion { get; set; }

    /// <summary>aEatStrPotion -- same posture as <see cref="EatLifePotion" />, for the Strength stat-potion family.</summary>
    public int EatStrPotion { get; set; }

    /// <summary>aEatDexPotion -- same posture as <see cref="EatLifePotion" />, for the Dexterity (AGI) stat-potion family.</summary>
    public int EatDexPotion { get; set; }

    /// <summary>
    ///     aEatElePotion -- same posture as <see cref="EatLifePotion" />, for the combined Elemental family.
    ///     Packs two independent sub-values (elemental-damage/elemental-defense) into this one raw counter --
    ///     see <see cref="Consumables.ElementalPotionPacking" /> for the divide/modulo-1000 convention. Never
    ///     read/written directly by anything that needs one specific sub-value; always go through that type.
    /// </summary>
    public int EatElePotion { get; set; }
}
