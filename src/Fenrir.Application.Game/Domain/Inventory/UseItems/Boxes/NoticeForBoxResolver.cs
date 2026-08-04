using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

public static class NoticeForBoxResolver
{
    public static NoticeDecision Decide(int rewardItemId)
    {
        var shouldBroadcast = LootBoxCatalog.NoticeRewardWhitelist.Contains(rewardItemId) || IsWarlord5(rewardItemId);
        return new NoticeDecision(shouldBroadcast, shouldBroadcast);
    }

    private static bool IsWarlord5(int itemId)
    {
        return itemId is >= 87013 and <= 87020
            or >= 87034 and <= 87041
            or >= 87055 and <= 87062
            or >= 87077 and <= 87084
            or >= 87099 and <= 87106
            or >= 87121 and <= 87128;
    }

    public readonly record struct NoticeDecision(bool ShouldBroadcast, bool WriteEliteGainAudit);
}
