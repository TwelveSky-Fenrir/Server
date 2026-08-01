using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

public static class NoticeForBoxResolver
{
    public const byte EliteItemTypeThreshold = 4;

    public static NoticeDecision Decide(int boxId, int rewardItemId, byte rewardItemType)
    {
        var whitelisted = LootBoxCatalog.NoticeRewardWhitelist.Contains(rewardItemId);

        if (LootBoxCatalog.EliteOnlyNoticeBoxIds.Contains(boxId))
        {
            var elite = rewardItemType >= EliteItemTypeThreshold;
            return new NoticeDecision(elite && whitelisted, elite);
        }

        return new NoticeDecision(whitelisted, false);
    }

    public readonly record struct NoticeDecision(bool ShouldBroadcast, bool WriteEliteGainAudit);
}
