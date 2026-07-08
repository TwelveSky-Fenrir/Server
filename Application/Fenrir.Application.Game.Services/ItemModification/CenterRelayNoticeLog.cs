using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Shared, log-only stand-in for the legacy <c>mCENTER.U_ZONE_BROADCAST_FOR_CENTER_SEND</c> relay calls
///     used by <c>EnchantItemService</c> (enchant-cap notices, relay sorts 115/2001) and
///     <c>CraftItemService</c>/<c>CraftSkillBookService</c> (the shared <c>MakeNotice</c> helper, relay sort
///     2000) -- see each call site's own remarks for why these three are treated identically and distinctly
///     from <see cref="Chat.WorldNoticeService" />/<c>BroadcastNotice</c>'s relay sort 102.
/// </summary>
/// <remarks>
///     <para>
///         Unlike relay sort 102 (<c>RELAY_GENERAL_NOTICE_SEND</c>, see <see cref="Chat.WorldNoticeService" />'s
///         own remarks), sorts 102-115 are the only cases <c>MyWork::ProcessForRelay</c> handles
///         (Server/ts25zone/S04_MyWork04.cpp:19-301, every case ends in a hard <c>return</c>, no default
///         label, no trailer after the switch) -- sorts 2000/2001 have NO matching case anywhere in that
///         function, and sort 115 itself is already claimed there by an unrelated payload shape
///         (<c>WORLD_CHAT_RECV</c>, case 115 "[TRIBE_NOTIFY_ALL]", :287-299) that does not match the 2-field
///         (tribe, name) payload <c>U_ZONE_BROADCAST_FOR_CENTER_SEND(115, tTribe, tName)</c> actually builds
///         for the wing enchant-cap call (S04_MyWork02.cpp:3247). Tracing the true receiving-side behavior
///         for 115/2000/2001 (and resolving that apparent sort collision) requires further archaeology inside
///         <c>ts25center</c> itself, which was not available in the pass that produced this file -- rather
///         than guess a byte-exact client-facing packet, this collapses to a structured log line, the SAME
///         precedent already established for <c>Zone.AnnounceEliteBossDefeated</c> (relay sort 2003, also no
///         matching <c>ProcessForRelay</c> case) and <c>ZoneEventBroadcaster.AnnounceTowerStatus</c>'s sibling
///         751/752/753/754/755 family.
///     </para>
///     <para>
///         The item-type gate below (<c>iType &gt;= 4</c>) is <c>MakeNotice</c>'s own documented DEFAULT-case
///         fallback rule (Server/ts25zone/S04_MyWork02.cpp:482-486) -- it is NOT the full rule: <c>MakeNotice</c>
///         also unconditionally notifies for a large hardcoded item-id allow-list (pets/mounts/Legendary
///         Pet/Guardian Pet/amulets/animal-tier items, :345-481) that is not reproduced here because several
///         of its entries are symbolic constants (<c>ANIMAL_NUM_*</c>) whose numeric values were not resolved
///         in this pass. This means a low-type item that IS on that allow-list will incorrectly not be logged
///         here -- flagged for a follow-up legacy-behavior-translator pass with the resolved allow-list.
///     </para>
/// </remarks>
internal static class CenterRelayNoticeLog
{
    /// <summary>
    ///     <c>MakeNotice</c>'s own default-case fallback threshold (Server/ts25zone/S04_MyWork02.cpp:483-485,
    ///     <c>if (sITEM_INFO-&gt;iType &lt; 4) return;</c>) -- matches <c>EliteItemType</c> in
    ///     <c>EnchantResolver</c>.
    /// </summary>
    private const byte NotableItemTypeThreshold = 4;

    /// <summary>Enchant-cap threshold shared by both branches below (DEFINE.h:613, <c>MAX_IMPROVE_ITEM_NUM</c> = 40).</summary>
    public const int EnchantCapValue = 40;

    /// <summary>
    ///     Stand-in for CraftItem/CraftSkillBook's shared <c>MakeNotice</c> helper (Server/ts25zone/
    ///     S04_MyWork02.cpp:309-493) on a qualifying recipe success. <paramref name="recipeLabel" /> is a
    ///     Fenrir-side description of which recipe fired, for log correlation only -- it has no legacy
    ///     equivalent and does not gate anything.
    /// </summary>
    public static void LogNotableCraft(ILogger logger, WorldDataCache worldData, byte tribe, string characterName,
        int resultItemId, string recipeLabel)
    {
        if (!worldData.ItemsById.TryGetValue(resultItemId, out var definition) ||
            definition.Item.Type < NotableItemTypeThreshold)
            return;

        logger.LogInformation(
            "Notable-craft notice (legacy MakeNotice, Center relay sort 2000, not client-broadcast -- see " +
            "CenterRelayNoticeLog remarks): tribe {Tribe} character {CharacterName} crafted {ResultItemId} " +
            "({ResultItemName}) via {RecipeLabel}",
            tribe, characterName, resultItemId, definition.Item.Name, recipeLabel);
    }

    /// <summary>
    ///     Stand-in for EnchantItem's enchant-cap broadcast (Server/ts25zone/S04_MyWork02.cpp:3244-3247 wing,
    ///     :3350-3357 non-wing). <paramref name="isWing" /> selects only the log wording/relay-sort cited, it
    ///     does not change the threshold check the caller already performed.
    /// </summary>
    public static void LogEnchantCap(ILogger logger, byte tribe, string characterName, int enchantValue, bool isWing)
    {
        logger.LogInformation(
            "Enchant-cap notice (legacy relay sort {RelaySort}, not client-broadcast -- see CenterRelayNoticeLog " +
            "remarks): tribe {Tribe} character {CharacterName} reached enchant {EnchantValue} on a {ItemKind} item",
            isWing ? 115 : 2001, tribe, characterName, enchantValue, isWing ? "wing" : "non-wing");
    }
}
