using System.Collections.Immutable;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tribes;

/// <summary>
///     Posted by <c>TribeActionHandler</c> AFTER a TRIBE_WORK self-mutation (doc 10 §2) is already decided,
///     to mirror it onto the ACTOR'S OWN <see cref="World.PlayerRuntimeState" /> -- same "pre-decided
///     result, tick just applies it" contract as <see cref="Inventory.InventoryZoneCommand" />/
///     <see cref="Skills.SkillZoneCommand" />. Every field is nullable/optional and independently applied;
///     null means "this tSort did not touch this field," not "reset to zero" (see
///     <see cref="World.Zone.ApplyTribeProgressCommand" />).
/// </summary>
/// <param name="CharacterId">
///     Which player this applies to -- a no-op if they already left the zone. Usually the requester
///     themselves, but <see cref="TribeRole" /> is the one field that targets a DIFFERENT character
///     (TRIBE_WORK tSort 2/3's sub-master target) -- same cross-character-mirror posture as
///     <see cref="Guilds.GuildMembershipZoneCommand" />, reused here rather than adding a second command
///     type for a single field.
/// </param>
/// <param name="ContributionPoints">
///     New CP balance (aKillOtherTribe) -- tSort 5/6/7/11/16/17 debit this (report 04 §5's
///     existing field).
/// </param>
/// <param name="TribeRole">
///     New <c>ReturnTribeRole</c> encoding (0 member, 1 master, 2 sub-master -- see
///     <see cref="World.PlayerRuntimeState.TribeRole" />). TRIBE_WORK tSort 2 (appoint, -&gt; 2) / tSort 3
///     (remove, -&gt; 0) mirror this onto the SUB-MASTER TARGET, not the requesting Force Leader, so that
///     character's next TRIBE_WORK action (tSort 4/16/17/18's role gates) sees the change without a re-log.
/// </param>
/// <param name="Title">New aTitle (tSort 6, USE_TITLE tier purchase).</param>
/// <param name="Halo">New aHalo (tSort 7, USE_HALO enchant success/failure).</param>
/// <param name="ProtectForHalo">
///     New aProtectForHalo charge count (tSort 7's protection-consumed branch) -- session-scoped
///     only, see <see cref="World.PlayerRuntimeState.ProtectForHalo" />'s own remarks.
/// </param>
/// <param name="UseOrnament">
///     New aUseOrnament flag (tSort 9/10) -- session-scoped only, see
///     <see cref="World.PlayerRuntimeState.UseOrnament" />'s own remarks.
/// </param>
/// <param name="BonusItemLevel">
///     New aBonusItemLevel (tSort 8, always reset to 0 after a successful claim) --
///     session-scoped only.
/// </param>
/// <param name="BonusItemValue">
///     New aBonusItemValue (tSort 8's companion flag, always reset to false after a successful
///     claim) -- session-scoped only.
/// </param>
/// <param name="StatVit">New spent Vitality (tSort 1 stat reset).</param>
/// <param name="StatStr">New spent Strength (tSort 1 stat reset).</param>
/// <param name="StatInt">New spent Intelligence/Ki (tSort 1 stat reset).</param>
/// <param name="StatDex">New spent Dexterity/Wisdom (tSort 1 stat reset).</param>
/// <param name="StatPoints">New unspent stat points (tSort 1 refunds the 4 spent stats back into this pool).</param>
/// <param name="Life">New current Life (tSort 1 forces this to 1, matching the legacy's own <c>aLifeValue=1</c>).</param>
/// <param name="Mana">New current Mana (tSort 1 forces this to 0, matching the legacy's own <c>aManaValue=0</c>).</param>
/// <param name="UpdatedStats">
///     Freshly recomputed <see cref="EffectiveStats" /> whenever a stat-affecting field above
///     changed (<see cref="Inventory.EquipmentService.RecomputeStats" />) -- null otherwise.
/// </param>
/// <param name="DropItems">
///     Ground-item drop(s) to spawn at the character's CURRENT (tick-thread-fresh) position when applied --
///     tSort 4/8/16/17/18's <c>ProcessForDropItem</c> calls (doc 10 §2). Spawning mutates
///     <see cref="World.Zone" />'s own tick-owned ground-item state, so <see cref="World.Zone.SpawnGroundItem" />
///     is <c>internal</c> (not thread-safe from a request thread) and this is the handlers' only avenue to
///     request a drop. Fenrir's ground-item pool has no capacity limit (unlike the legacy's
///     <c>ProcessForDropItem</c>, which could fail), so that failure path is unreachable here.
/// </param>
/// <param name="Applied">
///     Completed by <see cref="World.Zone.ApplyTribeProgressCommand" /> once actually mirrored -- see
///     <see cref="Inventory.InventoryZoneCommand.Applied" />'s own remarks for why this matters while
///     <see cref="World.PlayerRuntimeState.EconomyActionLock" /> is held.
/// </param>
public readonly record struct TribeProgressZoneCommand(
    int CharacterId,
    int? ContributionPoints = null,
    byte? TribeRole = null,
    int? Title = null,
    int? Halo = null,
    int? ProtectForHalo = null,
    bool? UseOrnament = null,
    int? BonusItemLevel = null,
    bool? BonusItemValue = null,
    int? StatVit = null,
    int? StatStr = null,
    int? StatInt = null,
    int? StatDex = null,
    int? StatPoints = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    ImmutableArray<TribeGroundItemDrop> DropItems = default,
    TaskCompletionSource? Applied = null);

/// <summary>One ground-item drop request -- see <see cref="TribeProgressZoneCommand.DropItems" />.</summary>
public readonly record struct TribeGroundItemDrop(int ItemId, int Quantity);
