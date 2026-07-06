namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Banked counters for the <c>CZ_USE_INVENTORY_ITEM_SEND</c> (op 23) consumable families added on top of
///     the pre-existing Bottle/Guild-Scroll/Faction-Transfer/GP-ticket dispatch -- loot-box LOD round counter,
///     protection/enhancement charges, and the two unambiguous banked cash-timer ids (Faction Notice, Taiyan
///     Key). Every field here is session-scoped only, same open issue as <see cref="ProtectForHalo" />/
///     <see cref="TribeNotifyScrollCount" />/<see cref="UseOrnament" /> above: some of these (e.g. the
///     protection-charm counters) have no persisted game.Characters column at all yet, others (e.g. a future
///     Lucky Drop Scroll counter) would reuse an already-existing column (game.Characters.DropItemTime) that
///     is itself not yet wired through <c>CharacterWorldSnapshotDto</c>/<c>PlayerEnterData</c> -- durability
///     for this whole group is a follow-up for fenrir-database-engineer, not invented here.
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
    ///     game.Characters.DropItemTime already exists as a persisted column for this exact field, but is not
    ///     yet read at world entry -- see this type's own remarks.
    /// </summary>
    public int DropItemTime { get; set; }

    /// <summary>Taiyan Key (world.Item 1049) -- aZone125Time, a dungeon-access timer gated on the level cap.</summary>
    public int TaiyanKeyTimer { get; set; }
}
