using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

internal static class CenterRelayNoticeLog
{

        private const byte NotableItemTypeThreshold = 4;

        public const int EnchantCapValue = 40;

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

        public static void LogEnchantCap(ILogger logger, byte tribe, string characterName, int enchantValue, bool isWing)
    {
        logger.LogInformation(
            "Enchant-cap notice (legacy relay sort {RelaySort}, not client-broadcast -- see CenterRelayNoticeLog " +
            "remarks): tribe {Tribe} character {CharacterName} reached enchant {EnchantValue} on a {ItemKind} item",
            isWing ? 115 : 2001, tribe, characterName, enchantValue, isWing ? "wing" : "non-wing");
    }

        public static void LogWarlordSwap(ILogger logger, byte tribe, string characterName, int replacementItemId)
    {
        logger.LogInformation(
            "Warlord-swap notice (legacy MakeNotice, Center relay sort 2000, not client-broadcast -- see " +
            "CenterRelayNoticeLog remarks): tribe {Tribe} character {CharacterName} received replacement item " +
            "{ReplacementItemId}",
            tribe, characterName, replacementItemId);
    }
}
