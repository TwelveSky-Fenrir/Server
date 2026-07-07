using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     A player's in-memory, authoritative state while <c>InWorld</c>. Mutated only by <see cref="Zone.RunAsync" />
///     -- every other thread posts a <see cref="ZoneCommand" /> and waits for the next tick instead.
/// </summary>
public sealed partial class PlayerRuntimeState
{
    public required int CharacterId { get; init; }
    public required IPacketSession Session { get; init; }
    public required string Name { get; init; }
    public required byte Tribe { get; init; }
    public required byte Gender { get; init; }
    public required byte HeadType { get; init; }
    public required byte FaceType { get; init; }

    /// <summary>
    ///     aLevel1. Mutated only by <see cref="Zone.GrantMonsterKillExperience" /> on a level-up -- every other
    ///     read site treats this as the character's current level.
    /// </summary>
    public required short Level { get; set; }

    public short MapId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }

    /// <summary>
    ///     Spent base stat points (legacy aVit/aStr/aInt/aDex). Note: StatInt feeds the Ki stat and StatDex
    ///     feeds Wisdom in <see cref="StatCalculator" />, not Intelligence/Dexterity literally.
    /// </summary>
    public int StatVit { get; set; }

    public int StatStr { get; set; }
    public int StatInt { get; set; }
    public int StatDex { get; set; }

    /// <summary>Unspent stat/skill points (aStatPoint/aSkillPoint) — spend-on-levelup UI reads these, not StatCalculator.</summary>
    public int StatPoints { get; set; }

    public int SkillPoints { get; set; }

    /// <summary>
    ///     Learned-skill slots (legacy <c>aSkill[40][0..1]</c>) -- an absent slot is "empty," same convention
    ///     as <see cref="Inventory" />. Mutated only by <see cref="Zone" />'s own tick.
    /// </summary>
    public ImmutableDictionary<byte, LearnedSkill> LearnedSkills { get; set; } =
        ImmutableDictionary<byte, LearnedSkill>.Empty;

    /// <summary>Total XP (aExp1/aExp2 combined).</summary>
    public long Experience { get; set; }

    /// <summary>
    ///     aLevel2, the post-cap "high level" rebirth ladder (1-12, MAX_LIMIT_HIGH_LEVEL_NUM) -- only reachable
    ///     once <see cref="Level" /> reaches <see cref="Stats.LevelProgressionCalculator.MaxLevel" />. No
    ///     Fenrir-side kill-experience path grants this yet (see <see cref="Stats.LevelProgressionCalculator" />'s
    ///     own remarks); <c>TribeActionHandler.HandleRebirthAsync</c>'s Max Rebirth gate is its only consumer today.
    /// </summary>
    public short Level2 { get; set; }

    /// <summary>aExp2 -- Level2's own XP counter, reset to 0 on every successful Max Rebirth.</summary>
    public int Exp2 { get; set; }

    /// <summary>
    ///     aRebirthNum -- real cap is 6 (app-enforced by TribeActionHandler, not this field). Read by StatCalculator's
    ///     CriticalDefence and Critical wrapper bonuses.
    /// </summary>
    public int RebirthCount { get; set; }

    /// <summary>aTitle (category*100 + rank 1-14) -- read by StatCalculator's title-rank bonus tables.</summary>
    public int Title { get; set; }

    /// <summary>
    ///     aHalo -- read twice independently by StatCalculator: added directly to all 4 base stats, and again for its own
    ///     CriticalDefence bonus.
    /// </summary>
    public int Halo { get; set; }

    /// <summary>aKillOtherTribe (CP) -- quest reward type 3 income; not consumed by StatCalculator.</summary>
    public int ContributionPoints { get; set; }

    /// <summary>
    ///     aTeacherPoint -- quest reward type 5 income. A separate counter from the Mentor system's
    ///     TeacherCharacterId/StudentCharacterId bond.
    /// </summary>
    public int TeacherPoint { get; set; }

    /// <summary>
    ///     War Point stat -- wire-exposed via <c>AvatarInfo.WarPoint</c> (still always sent as 0 from
    ///     <c>AvatarInfoTemplates</c>, since this field isn't hydrated from a persisted source at zone entry
    ///     yet). Granted only via <see cref="Zone.GrantWarPoints" /> -- the Regular-War-host kill-reward override
    ///     (<c>Zone.ApplyRegularWarCpOverride</c>) and the boss/event monster-drop tier
    ///     (<c>World.Loot.BossEventDropResolver</c>, identifiers 746/9001). Not yet persisted: no
    ///     <c>game.Characters</c> column/repository DTO carries this field today, so it resets to 0 on every
    ///     zone (re)entry until a follow-up adds one -- same "not modeled" posture
    ///     <c>World.Npcs.NpcShopPolicy</c>'s own remarks describe for the separate WarPoint-shop branch.
    /// </summary>
    public int WarPoint { get; set; }

    /// <summary>
    ///     DS/Blood Point stat (legacy <c>aBloodCoin</c>) -- wire-exposed via <c>AvatarInfo.BloodCoin</c> (same
    ///     "always sent as 0, not yet persisted" posture as <see cref="WarPoint" />). Granted only via
    ///     <see cref="Zone.GrantBloodPoints" />.
    /// </summary>
    public int BloodCoin { get; set; }

    /// <summary>
    ///     Cached output of <see cref="StatCalculator.ComputeEffectiveStats" /> -- null until first computed.
    ///     Recompute only on an equipment/buff/level/title/halo change event, never once per tick.
    /// </summary>
    public EffectiveStats? Stats { get; set; }

    /// <summary>
    ///     This character's item containers (inventory pages, equipment, store pages) while <c>InWorld</c> --
    ///     mutated only by <see cref="Zone" />'s own tick, same single-writer contract as every other field.
    /// </summary>
    public InventoryState Inventory { get; } = new();

    /// <summary>
    ///     Serializes every economy-affecting request-thread action for this character (NPC buy/sell, enchant,
    ///     craft) across its read-<see cref="Inventory" />-snapshot / await-SQL / post-mirror-command
    ///     sequence. Without this, two concurrent requests for the same character could both read the same
    ///     stale pre-mirror snapshot and duplicate an item or money. Acquire before reading
    ///     <see cref="Inventory" />, release only after the mirror command is posted.
    /// </summary>
    public SemaphoreSlim EconomyActionLock { get; } = new(1, 1);

    /// <summary>
    ///     Wall-clock instant of this character's last accepted op23 (CZ_USE_INVENTORY_ITEM_SEND) request --
    ///     the per-avatar, item-agnostic anti-flood gate mirroring legacy's single <c>mTickForUseInventoryItem</c>
    ///     field (Server/Header/H07_MyGame.h:901): one shared stamp across every item id, never a per-item-type
    ///     cooldown table. Defaulted to <see cref="DateTime.UtcNow" /> at construction time so a freshly
    ///     (re)registered avatar (every zone entry/transfer constructs a fresh <see cref="PlayerRuntimeState" />)
    ///     starts the gate the same way legacy (re)initializes it to the current tick at avatar registration
    ///     (Server/ts25zone/S04_MyWork02.cpp:877) -- the very first item-use attempt after entering a zone is
    ///     subject to the exact same gate as any later one. Checked and stamped by
    ///     <c>UseInventoryItemHandler</c>, before any item-specific dispatch, mirroring legacy's own ordering
    ///     (Server/ts25zone/S04_MyWork03.cpp:1861-1866).
    /// </summary>
    public DateTime LastItemUseUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Server-side monotonic counter, independent of the DB's own FlushSequence baseline -- incremented
    ///     once per accepted move (<c>Zone.PlayerLifecycle.HandleMove</c>) or once per call to
    ///     <see cref="MarkProgressDirty" />, never reset, so <c>usp_Character_PersistBatch</c> AND
    ///     <c>usp_Character_PersistProgressBatch</c> -- which share this single per-character column -- always
    ///     see a strictly increasing value for this character's lifetime in this zone.
    /// </summary>
    public long FlushSequence { get; set; }

    /// <summary>
    ///     Every Vitals/Progression dirty-mark site (combat, death/XP-loss, revive, skill cast/mana, quest/pet/
    ///     rebirth/CP mirrors) must route through here instead of calling <c>dirtyTracker.MarkDirty</c> directly.
    ///     Centralizing the <see cref="FlushSequence" /> bump here (rather than duplicating
    ///     <c>state.FlushSequence++;</c> at each of the ~25 call sites across 8 files) is what makes
    ///     <c>Fenrir.Application.Game.Hosting.World.ProgressWriteBehindHost</c>'s flush actually durable:
    ///     without it, a Vitals/Progression-only change with no accompanying move would never bump the shared
    ///     counter, so <c>usp_Character_PersistProgressBatch</c>'s "FlushSequence &gt; stored" guard would
    ///     silently no-op every progress flush after the very first one each session -- the exact
    ///     zero-net-cost item/CP-duplication shape this fix closes. Safe to call unsynchronized: every one of
    ///     these call sites already runs on this character's own zone tick thread (single-writer), the same
    ///     contract <see cref="FlushSequence" /> and every other field on this type already relies on. Position
    ///     keeps its own separate, pre-existing <c>state.FlushSequence++;</c> inline in <c>HandleMove</c> --
    ///     deliberately not routed through here, so its historical exactly-once-per-accepted-move cadence is
    ///     untouched by this change.
    /// </summary>
    public void MarkProgressDirty(DirtyTracker<int> dirtyTracker, DirtyFlags flags)
    {
        FlushSequence++;
        dirtyTracker.MarkDirty(CharacterId, flags);
    }
}
