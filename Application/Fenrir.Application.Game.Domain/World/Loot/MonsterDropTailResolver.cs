namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Ports the two live drop tiers from the tail of <c>ProcessForDropItem</c>
///     (<c>Server/ts25zone/S07_MyGame05.cpp:3000-3732</c>) that sit outside both <see cref="MonsterDropRoller" />'s
///     own documented <c>:2999</c> boundary and <see cref="BossEventDropResolver" />'s own documented
///     <c>:2333-2662</c> range: <see cref="ResolveCpGiftCard" /> ("CP Gift Card", <c>:3391-3427</c>) and
///     <see cref="ResolveRebirthItem" /> ("LOD Rebirth Item", <c>:3515-3582</c>). Every other block in that
///     ~700-line span (<c>DROP_ADD_STONE_ITEM</c>/<c>DROP_IMPROVE_STONE_ITEM</c>/<c>DROP_SKILL_PIECE_ITEM</c>/
///     <c>DROP_SKILL_BOX_ITEM</c>/<c>DROP_KEY_ITEM</c>/<c>DROP_LUCKY_ITEM</c>/<c>DROP_MONEY_BAR_ITEM</c>/the
///     seasonal-event blocks) is either commented out or gated by a macro never <c>#define</c>'d anywhere in
///     <c>Server/</c> -- dead code, not ported here.
/// </summary>
/// <remarks>
///     <para>
///         <b>Correction to this class's own originating finding:</b> that finding asserted
///         <c>mCheckZone126TypeServer</c>/<c>mCheckZone039TypeServer</c> are "permanently false in every real
///         build." That is wrong -- <c>#define ZONE126</c>/<c>#define ZONE039</c> both exist, unconditionally,
///         at <c>Server/ts25zone/H07_MyGame.h:7,15</c>, so the assignment sites
///         (<c>Server/ts25zone/S07_MyGame01.cpp:821-834,895-913</c>) do compile and set these flags true for
///         specific legacy server numbers (39/144/145/313/74 for Zone039; 210-218 for Zone126). Whether any
///         Fenrir shard maps onto one of those legacy server numbers is a separate, still-open question --
///         Fenrir shards by map id (<c>admin.ShardMapAssignments</c>), not by the legacy
///         <c>mSERVER_INFO.mServerNumber</c> scheme, and nothing in this codebase correlates the two today.
///         Per this repo's "no legacy parity from memory" rule, <see cref="ResolveCpGiftCard" />'s
///         <c>isZone126TypeShard</c> parameter is therefore left for the caller to supply rather than resolved
///         internally -- <see cref="Monsters.MonsterSpawnScheduler" /> currently passes a hardcoded
///         <c>false</c> pending that mapping work, the same posture <see cref="MonsterDropRoller.IsEligible" />
///         already takes for its own unported Level2 branch ("no such field exists yet"). Zone241, by
///         contrast, IS already resolvable in Fenrir (<see cref="Zone.IsZone241TypeZone" />,
///         <see cref="GameServerOptions.Zone241DungeonMapIds" />), which is why
///         <see cref="ResolveCpGiftCard" />'s Zone241 carve-out (see Edge case below) is fully wired rather
///         than left as an open parameter.
///     </para>
///     <para>
///         <b>Deliberate divergence -- the shared, unreset "staged item" bug is NOT reproduced.</b> The C++
///         source stages "the item about to drop" in one function-scoped variable (<c>tItemIndex</c>,
///         <c>S07_MyGame05.cpp:2118</c>) reused across every tier in <c>ProcessForDropItem</c>, never cleared
///         after a tier consumes it. <see cref="ResolveRebirthItem" />'s own chest-item check only assigns a
///         fresh value when its draw is under 20 (<c>:3569-3576</c>); for a draw of 20+ it falls through to
///         "if tItemIndex != 0" (<c>:3577</c>) without clearing it first -- so on a kill where the CP Gift
///         Card tier's own gate also succeeded (leaving a gift-card id staged) AND the Rebirth tier's own
///         monster-id gate matches AND its draw lands >= 20, legacy silently re-drops the already-granted gift
///         card a second time. This reads as legacy data-hygiene bug, not deliberate design, and the behavior
///         contract this class was built from explicitly leaves reproducing it as an open decision for the
///         receiving engineer rather than mandating it. Given this touches economy balance, the conservative
///         choice taken here is NOT to reproduce it: <see cref="ResolveCpGiftCard" /> and
///         <see cref="ResolveRebirthItem" /> are independent, side-effect-free pure functions with no shared
///         staging variable, so this duplication is structurally impossible here. Flagged for
///         re-verification if a future audit determines byte-for-byte parity (including this bug) is actually
///         required.
///     </para>
///     <para>
///         <b>Open gap, not modeled:</b> both live calls pass <c>ReturnItemSerial(4)</c> (i.e.
///         <c>ReturnItemSerial(IELITE)</c>, per <c>Server/Header/Protocol/STRUCT.h:1658</c>) as
///         <c>ProcessForDropItem</c>'s <c>tItemRecognitionNumber</c> parameter (<c>Server/ts25zone/H07_MyGame.h:542</c>
///         for the parameter order), where the CP Gift Card call two blocks above passes a plain <c>0</c> for
///         that same slot (<c>:3424</c>). <c>ReturnItemSerial</c>'s own body
///         (<c>Server/Header/function.h:5-26</c>) confirms <c>IELITE</c> is one of the four
///         <c>tItemType</c> values that produce a real (non-zero), current-timestamp-derived serial rather than
///         the default-case <c>0</c> -- so this is a genuine, deliberate "stamp this drop with a serial"
///         signal, not dead code, but nothing in <see cref="GroundItemEntity" />'s own shape today has an
///         equivalent field to carry it into. Not modeled here; flagged rather than assumed inert, per the
///         behavior contract's own instruction.
///     </para>
/// </remarks>
public static class MonsterDropTailResolver
{
    private const int CpGiftCard5ItemId = 691;
    private const int CpGiftCard10ItemId = 692;
    private const int CpGiftCard15ItemId = 693;
    private const int CpGiftCard20ItemId = 694;

    /// <summary><c>cpgRateLv</c>'s "Zone126-type" branch (<c>S07_MyGame05.cpp:3402</c>).</summary>
    private const int CpGiftBaseRateZone126 = 50;

    /// <summary><c>cpgRateLv</c>'s default branch (<c>S07_MyGame05.cpp:3402</c>).</summary>
    private const int CpGiftBaseRateDefault = 25;

    /// <summary>
    ///     MAX_LIMIT_HIGH_LEVEL_NUM (<c>Server/Header/Protocol/DEFINE.h:605</c>, equals 12) -- Level2's own cap;
    ///     <see cref="ResolveCpGiftCard" />'s rate is halved unless the killer sits exactly at this value
    ///     (<c>S07_MyGame05.cpp:3403-3404</c>). Kept as a local copy rather than referencing
    ///     <see cref="Fenrir.Application.Game.Domain.Consumables.StatPotionResolver.RequiredLevel2" /> -- this codebase's own
    ///     established
    ///     convention for this constant (see e.g. <see cref="Progression.RebirthProgression.MaxHighLevel" />,
    ///     shared only between the two rebirth-advancement trigger paths that consume it) is one private copy
    ///     per unrelated consuming file, not a single cross-namespace constant reused everywhere.
    /// </summary>
    private const int MaxHighLevel = 12;

    /// <summary>
    ///     Zone241 "LOD" carve-out (<c>S07_MyGame05.cpp:2224-2229</c>): the general drop-eligibility flag is
    ///     forced true for this narrow monster-id band on a Zone241-type shard, regardless of level gap --
    ///     confirmed to affect only this range, not <see cref="ResolveRebirthItem" />'s own wider three-band
    ///     gate.
    /// </summary>
    private const int Zone241ForcedEligibleMonsterIdLow = 725;

    private const int Zone241ForcedEligibleMonsterIdHigh = 730;

    private const int RebirthItemId = 1444;
    private const int RebirthChestLowTierItemId = 1378;
    private const int RebirthChestMidTierItemId = 1379;

    /// <summary>
    ///     <c>DROP_CP_GIFT</c> (<c>S07_MyGame05.cpp:3391-3427</c>): one of four "CP Gift Card" items, gated on
    ///     the general drop-eligibility flag (<paramref name="generalDropEligible" />, or its Zone241 forced-true
    ///     carve-out below) and the killer's post-cap Level2 being above zero.
    /// </summary>
    /// <param name="generalDropEligible">
    ///     <c>tCheckPossibleDrop</c> as already computed for the rest of this kill's generic pipeline --
    ///     callers should pass <see cref="MonsterDropRoller.IsEligible" />'s own result for this same monster
    ///     and killer.
    /// </param>
    /// <param name="monsterId">The killed monster's numeric id -- only consulted for the Zone241 carve-out.</param>
    /// <param name="isZone241TypeShard">This shard's <see cref="Zone.IsZone241TypeZone" />.</param>
    /// <param name="killerLevel2">The killer's post-cap "Level2"/rebirth-ladder value (0-12).</param>
    /// <param name="isZone126TypeShard">See this class's own remarks for why this is caller-supplied, not resolved here.</param>
    public static IReadOnlyList<DroppedItem> ResolveCpGiftCard(bool generalDropEligible, int monsterId,
        bool isZone241TypeShard, int killerLevel2, bool isZone126TypeShard, Random random)
    {
        var forcedEligible = isZone241TypeShard &&
                             monsterId is >= Zone241ForcedEligibleMonsterIdLow
                                 and <= Zone241ForcedEligibleMonsterIdHigh;

        if ((!generalDropEligible && !forcedEligible) || killerLevel2 <= 0)
            return [];

        var rate = isZone126TypeShard ? CpGiftBaseRateZone126 : CpGiftBaseRateDefault;
        if (killerLevel2 != MaxHighLevel)
            rate /= 2;

        if (LootRandomSource.RandomNumber(random) > rate)
            return [];

        // rand_mir() % 100 -- a flat draw, distinct in shape from the weighted gate roll above (see
        // LootRandomSource's own remarks on why this pipeline's rolls are never uniform-by-default).
        var selection = random.Next(0, 100);
        var itemId = selection switch
        {
            < 80 => CpGiftCard5ItemId,
            < 90 => CpGiftCard10ItemId,
            < 95 => CpGiftCard15ItemId,
            _ => CpGiftCard20ItemId
        };

        return [new DroppedItem(itemId, 1)];
    }

    /// <summary>
    ///     <c>DROP_REBIRTH_ITEM</c> (<c>S07_MyGame05.cpp:3515-3582</c>): the "LOD" instance-boss rebirth-material
    ///     drop. Gated only on <paramref name="monsterId" /> falling in one of three fixed bands -- unlike
    ///     <see cref="ResolveCpGiftCard" />, this tier does NOT consult the general drop-eligibility flag at
    ///     all, so an over-level kill of one of these bosses still rolls it.
    /// </summary>
    public static IReadOnlyList<DroppedItem> ResolveRebirthItem(int monsterId, Random random)
    {
        if (!IsLodBossMonster(monsterId))
            return [];

        // rand_mir() % 100 -- ONE draw feeds both checks below (S07_MyGame05.cpp:3545), not two independent rolls.
        var roll = random.Next(0, 100);

        List<DroppedItem>? items = null;

        if (roll < 30)
            (items ??= []).Add(new DroppedItem(RebirthItemId, 1));

        if (roll < 7)
            (items ??= []).Add(new DroppedItem(RebirthChestLowTierItemId, 1));
        else if (roll < 20)
            (items ??= []).Add(new DroppedItem(RebirthChestMidTierItemId, 1));

        return items ?? [];
    }

    private static bool IsLodBossMonster(int monsterId)
    {
        return monsterId is >= 725 and <= 730 or >= 736 and <= 738 or >= 748 and <= 750;
    }
}
