using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Shared, log-only stand-in for the legacy <c>mCENTER.U_ZONE_BROADCAST_FOR_CENTER_SEND</c> relay calls
///     used by <c>EnchantItemService</c> (enchant-cap notices, relay sorts 115/2001) and
///     <c>CraftItemService</c>/<c>CraftSkillBookService</c>/<c>CraftPetService</c> (the shared
///     <c>MakeNotice</c> helper, relay sort 2000) -- see each call site's own remarks for why these are
///     treated identically and distinctly from <see cref="Chat.WorldNoticeService" />/<c>BroadcastNotice</c>'s
///     relay sort 102.
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
///         for the wing enchant-cap call (S04_MyWork02.cpp:3247).
///     </para>
///     <para>
///         2026-07-11 CLOSED (was: "requires further archaeology inside ts25center itself, not available in
///         the pass that produced this file"): a follow-up legacy-behavior-translator pass traced the true
///         receiving-side behavior for 115/672/2000/2001 and confirmed there is no wording to recover, for
///         any of them, ever -- this is not a pending citation, it is a dead end baked into the shipped
///         legacy binary. Both processes' broadcast-info receivers stub every one of these sorts as an empty
///         <c>break;</c> with no <c>default:</c> label in either switch to catch them instead (verified by a
///         file-wide grep, not just the case labels): <c>ts25center/S04_MyWork02.cpp</c> case 115 (:805-806)
///         and case 672 (:1079-1080); <c>ts25zone/S07_MyGame08.cpp</c>'s <c>ProcessForBroadcastInfo</c> case
///         115 (:688-689) and case 672 (:1158-1159). A repo-wide grep for <c>case 2000</c>/<c>case 2001</c>
///         found zero matches in either receiving switch (only 2 unrelated hits elsewhere in the tree: a
///         commented-out line and an unrelated item-drop/probability switch). The apparent sort-115 collision
///         with <c>ProcessForRelay</c>'s genuinely-implemented "[TRIBE_NOTIFY_ALL]" case (:287-299, which casts
///         a <c>WORLD_CHAT_RECV</c> payload, not this notice family's tribe+name/tribe+value shape) is also
///         confirmed moot: that function is reached only via the distinct <c>W_BROADCAST_DATA</c> wire
///         message, never <c>W_BROADCAST_INFO</c> -- the one this notice family's sends
///         (<c>U_ZONE_BROADCAST_FOR_CENTER_SEND</c>, <c>S06_MyUpperCom02.cpp:567-582</c>) actually use
///         (dispatch confirmed at <c>S06_MyUpperCom02.cpp:328-331</c>). The send-side structs themselves
///         (<c>ELITE_NOTICE</c>/<c>ENCHANT_NOTICE</c>, <c>Server/Header/Protocol/STRUCT.h:1306-1319</c>) also
///         carry no string/message field to translate -- numeric type/tribe/value/box fields and an avatar
///         name only. Rather than guess a byte-exact client-facing packet that the legacy server itself never
///         finished, this stays a structured log line permanently, the SAME precedent already established for
///         <c>Zone.AnnounceEliteBossDefeated</c> (relay sort 2003, also no matching <c>ProcessForRelay</c>
///         case) and <c>ZoneEventBroadcaster.AnnounceTowerStatus</c>'s sibling 751/752/753/754/755 family.
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
    ///     Stand-in for CraftItem/CraftSkillBook/CraftPet's shared <c>MakeNotice</c> helper (Server/ts25zone/
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

    /// <summary>
    ///     Stand-in for the High-Item "Warlord reroll" swap's own unconditional notice-broadcast attempt
    ///     (Server/ts25zone/S04_MyWork02.cpp:4092-4095), which fires through the SAME shared <c>MakeNotice</c>
    ///     helper (relay sort 2000) as <see cref="LogNotableCraft" /> -- see
    ///     <see cref="Fenrir.Application.Game.Domain.Forge.WarlordRerollBonusTable.NoticeReachesRecipients" />
    ///     for why, net of <c>MakeNotice</c>'s own item-tier fallback, this only ever actually reaches anyone
    ///     for an elite-tier swap in practice (callers are expected to gate on that method before calling this
    ///     one).
    /// </summary>
    public static void LogWarlordSwap(ILogger logger, byte tribe, string characterName, int replacementItemId)
    {
        logger.LogInformation(
            "Warlord-swap notice (legacy MakeNotice, Center relay sort 2000, not client-broadcast -- see " +
            "CenterRelayNoticeLog remarks): tribe {Tribe} character {CharacterName} received replacement item " +
            "{ReplacementItemId}",
            tribe, characterName, replacementItemId);
    }
}
