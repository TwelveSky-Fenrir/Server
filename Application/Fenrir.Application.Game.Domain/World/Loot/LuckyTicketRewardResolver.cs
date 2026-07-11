using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     The Lucky Ticket family's (world.Items 1035/1036/1037) reward-tier draw: per-ticket roll thresholds,
///     the roll/level tier cascade, the character-level/evolution-tier item-level window, and the fixed
///     per-ticket family serial -- everything <see cref="GeneralItemDropResolver.Resolve" /> itself does not
///     already own. The actual (Level,Type,Sort) catalog search is delegated to
///     <see cref="GeneralItemDropResolver.Resolve" />, parameterized so the cape slot is included only for the
///     IRARE tier and the skill-book slot is never included -- deliberately narrower than that resolver's own
///     unconditional-both-true default, which is correct only for the general monster-item-drop call site.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3342-3481 (the whole Lucky Ticket case block: thresholds,
///     tier cascade, retry loop, family serial); :3349-3350 (item-level window via <c>GetLLevel</c>/
///     <c>GetHLevel</c>); :3353-3364 (the three per-ticket threshold pairs); :3418-3459 (the roll/level tier
///     cascade, including the deployment-stage-gated top tier); :3461-3462 (cape-slot pool widening, IRARE-tier
///     only); :3464-3476 (the up-to-3-attempt retry loop and generic failure signal on exhaustion); :3478-3479
///     (the fixed-constant family serial formula, contrasted with the timestamp-derived
///     <c>ReturnItemSerial</c> used at nearly every other call site of the same granting mechanism).
///     Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:1141-1157,1159-1175 (<c>GetLLevel</c>/<c>GetHLevel</c>).
///     Server/Header/Protocol/STRUCT.h:1654-1660 (the <c>FITEM_TYPE</c> rarity-tier enum this resolver's
///     <see cref="Common" />/<see cref="Unique" />/<see cref="Rare" />/<see cref="Elite" /> constants mirror).
///     <para>
///         The whole switch/case block this ports is unconditionally live in the one real buildable
///         configuration (<c>WUSE_ITEM_1035</c> is unconditionally <c>#define</c>d --
///         Server/Header/use_inventory.h:1-14, Server/ts25zone/S04_MyWork03.cpp:6). A textually adjacent
///         sub-case for item 17124 in the same legacy switch is gated by <c>#ifndef LNW33</c>, which never
///         compiles in the only real buildable configuration (<c>LNW33</c> is always active there) -- it is
///         confirmed dead code and deliberately not ported here at all (not even as an unreachable branch).
///     </para>
/// </remarks>
public static class LuckyTicketRewardResolver
{
    /// <summary>ICOMMON (STRUCT.h:1655) -- the default/fallback tier when no roll bracket promotes it.</summary>
    public const int Common = 1;

    /// <summary>IUNIQUE (STRUCT.h:1656).</summary>
    public const int Unique = 2;

    /// <summary>IRARE (STRUCT.h:1657) -- the only tier that widens the sort pool to include the cape slot.</summary>
    public const int Rare = 3;

    /// <summary>
    ///     IELITE (STRUCT.h:1658) -- reachable only from the rarest roll bracket, only at character level 100+,
    ///     and only when <c>eliteTierEnabled</c> is true (mirrors <c>mSERVER_INFO.mDeploymentStage &gt; 0</c>,
    ///     a runtime INI-loaded server setting -- Server/Header/api.h:179, Server/Header/ini.h:71 -- not a
    ///     compile-time constant). See <see cref="ShippedProductionEliteTierEnabled" />.
    /// </summary>
    public const int Elite = 4;

    /// <summary>
    ///     MAX_LIMIT_LEVEL_NUM (the same level ceiling <c>GetHLevel</c>/<c>MonsterDropRoller.RollGeneralItems</c>
    ///     clamp to elsewhere in this codebase).
    /// </summary>
    private const int MaxItemLevel = 145;

    /// <summary>
    ///     The roll is drawn from a uniform 0..9999 range (<c>rand_mir() % 10000</c>, S04_MyWork03.cpp:3418).
    /// </summary>
    private const int RollCeilingExclusive = 10000;

    /// <summary>The roll floor (inclusive) above which every ticket always falls back to the lowest tier.</summary>
    private const int TopBracketFloor = 9000;

    /// <summary>
    ///     Up to 3 total attempts at <see cref="GeneralItemDropResolver.Resolve" /> for the SAME resolved tier/
    ///     level-window/pool-widening decision (S04_MyWork03.cpp:3464-3471's own retry <c>while</c> loop) --
    ///     distinct from, and layered on top of, <see cref="GeneralItemDropResolver" />'s own internal 10-attempt
    ///     retry budget for a single call.
    /// </summary>
    private const int MaxDrawAttempts = 3;

    /// <summary>
    ///     The actual shipped production <c>Server/BuildEU33/ServerInfo.ini:52</c> value for
    ///     <c>mSERVER_INFO.mDeploymentStage</c> is 0 ("off"), so the IELITE branch is unreachable in the
    ///     currently-live configuration -- a roll that would otherwise promote to IELITE at level 100+ falls
    ///     back to IRARE instead (see <see cref="ResolveTier" />'s own remarks). This is a genuinely
    ///     operator-configurable runtime setting in legacy (not a compile-time macro), so callers should treat
    ///     it as "currently off, not permanently dead" -- <see cref="TryDraw" /> takes it as an explicit
    ///     parameter rather than hardcoding it, and this constant is only this resolver's own documented
    ///     "matches shipped production today" default for a caller that has no reason to differ.
    /// </summary>
    public const bool ShippedProductionEliteTierEnabled = false;

    /// <summary>The three per-ticket (first, second) roll thresholds (S04_MyWork03.cpp:3353-3364).</summary>
    public static bool TryGetThresholds(int ticketItemId, out int firstThreshold, out int secondThreshold)
    {
        switch (ticketItemId)
        {
            case 1035: // Lucky Ticket
                firstThreshold = 1;
                secondThreshold = 300;
                return true;
            case 1036: // Big Lucky Ticket
                firstThreshold = 2;
                secondThreshold = 400;
                return true;
            case 1037: // God Lucky Ticket
                firstThreshold = 3;
                secondThreshold = 500;
                return true;
            default:
                firstThreshold = 0;
                secondThreshold = 0;
                return false;
        }
    }

    /// <summary>
    ///     The fixed, ticket-family-specific serial stamped onto a granted (non-stackable) reward --
    ///     <c>100000001 + (ticketItemId - 1035)</c> (S04_MyWork03.cpp:3478) -- NOT the standard
    ///     timestamp-derived <c>ReturnItemSerial</c> used at nearly every other call site of the same granting
    ///     mechanism. Only meaningful for a valid ticket id; callers should gate on <see cref="TryGetThresholds" />
    ///     (or <see cref="TryDraw" />) first.
    /// </summary>
    public static int ResolveFamilySerial(int ticketItemId)
    {
        return 100000001 + (ticketItemId - 1035);
    }

    /// <summary>
    ///     <c>GetLLevel</c>/<c>GetHLevel</c> (GameSystem_02_Item.cpp:1141-1175): below the character's first
    ///     evolution tier, a +-5 window around the character's own level, clamped to a floor of 1 and a
    ///     ceiling of <see cref="MaxItemLevel" />; once an evolution tier is reached, both bounds collapse to
    ///     the single fixed value <paramref name="level1" /> + <paramref name="level2" />, with no spread.
    /// </summary>
    public static (int Low, int High) ResolveItemLevelWindow(int level1, int level2)
    {
        if (level2 < 1)
        {
            var low = Math.Max(level1 - 5, 1);
            var high = Math.Min(level1 + 5, MaxItemLevel);
            return (low, high);
        }

        var fixedLevel = level1 + level2;
        return (fixedLevel, fixedLevel);
    }

    /// <summary>
    ///     The roll/level tier cascade (S04_MyWork03.cpp:3418-3459). <paramref name="roll" /> is expected in
    ///     0..9999 (see <see cref="RollCeilingExclusive" />); <paramref name="eliteTierEnabled" /> mirrors
    ///     <c>mSERVER_INFO.mDeploymentStage &gt; 0</c> -- see <see cref="ShippedProductionEliteTierEnabled" />.
    /// </summary>
    public static int ResolveTier(int roll, int level1, bool eliteTierEnabled, int firstThreshold,
        int secondThreshold)
    {
        if (roll < firstThreshold)
        {
            return level1 switch
            {
                < 5 => Common,
                < 45 => Unique,
                < 100 => Rare,
                _ => eliteTierEnabled ? Elite : Rare
            };
        }

        if (roll < secondThreshold)
        {
            return level1 switch
            {
                < 5 => Common,
                < 45 => Unique,
                _ => Rare
            };
        }

        if (roll < TopBracketFloor)
            return level1 >= 5 ? Unique : Common;

        // Top 10% of the roll range: always the lowest tier, regardless of level.
        return Common;
    }

    /// <summary>
    ///     One full Lucky Ticket draw: resolves thresholds for <paramref name="ticketItemId" />, draws a single
    ///     roll, resolves the tier and item-level window once, then retries the shared
    ///     <see cref="GeneralItemDropResolver.Resolve" /> reward-selection step (cape-widened only for the
    ///     IRARE tier, skill book never included) up to <see cref="MaxDrawAttempts" /> times for that SAME
    ///     resolved tier/window/widening decision -- the roll and tier are drawn exactly once per use-attempt,
    ///     never re-rolled between retries (S04_MyWork03.cpp:3418-3471: <c>tRandomValue</c>/<c>tItemType</c> are
    ///     computed once, strictly before the retry <c>while</c> loop). Returns <see langword="false" /> (with
    ///     <paramref name="rewardItemId" /> 0) for an unrecognized <paramref name="ticketItemId" /> or when all
    ///     attempts fail to find an eligible reward -- both collapse to the same generic "use failed, ticket
    ///     kept" outcome one level up, exactly as legacy cannot distinguish them either.
    /// </summary>
    public static bool TryDraw(WorldDataCache worldData, Random random, int ticketItemId, byte previousTribe,
        int level1, int level2, bool eliteTierEnabled, out int rewardItemId)
    {
        if (!TryGetThresholds(ticketItemId, out var firstThreshold, out var secondThreshold))
        {
            rewardItemId = 0;
            return false;
        }

        var roll = random.Next(RollCeilingExclusive);
        var tier = ResolveTier(roll, level1, eliteTierEnabled, firstThreshold, secondThreshold);
        var (levelLow, levelHigh) = ResolveItemLevelWindow(level1, level2);
        var includeCape = tier == Rare;

        for (var attempt = 0; attempt < MaxDrawAttempts; attempt++)
        {
            if (GeneralItemDropResolver.Resolve(worldData, random, previousTribe, tier, levelLow, levelHigh,
                    includeCape, false) is { } candidate)
            {
                rewardItemId = candidate;
                return true;
            }
        }

        rewardItemId = 0;
        return false;
    }
}
