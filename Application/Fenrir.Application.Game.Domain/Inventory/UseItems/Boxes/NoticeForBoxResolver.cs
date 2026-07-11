using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

/// <summary>
///     The pure decision behind a loot-box open's post-success cluster-notice attempt (workstream C10). Two
///     rules combine: boxes 1035/1036/1037 broadcast only when the reward is elite-typed (and additionally
///     write a gain-item audit entry when it is), while every other box attempts the broadcast unconditionally
///     -- but in both cases the actual broadcast happens only when the reward id is in <c>NoticeForBox</c>'s own
///     whitelist (<see cref="LootBoxCatalog.NoticeRewardWhitelist" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:5089-5147 (<c>NoticeForBox</c>: the reward-id whitelist and
///     the center-relay elite-notice broadcast; silent return when the reward is not whitelisted) ;
///     Server/ts25zone/S04_MyWork05.cpp:5408-5428 (the 1035/1036/1037 elite-only gate and the extra gain-item
///     audit entry). The elite-typed threshold (item Type &gt;= 4) is <c>MakeNotice</c>'s own notable-item
///     floor (Server/ts25zone/S04_MyWork02.cpp:483-485), the same constant <c>CenterRelayNoticeLog</c> and
///     <c>EnchantResolver</c> use.
///     <para>
///         <see cref="Decide" /> currently never elects to broadcast because
///         <see cref="LootBoxCatalog.NoticeRewardWhitelist" /> is empty (the concrete whitelist ids are not in
///         the C10 contract's cited ranges, and are not invented) -- and even once populated, the actual
///         cluster-wide broadcast wire seam is a separate hand-off (see this workstream's wiring manifest and
///         the log-only <c>CenterRelayNoticeLog</c> precedent). The elite-gain-audit flag, by contrast, is a
///         local decision the handler can act on today.
///     </para>
/// </remarks>
public static class NoticeForBoxResolver
{
    /// <summary><c>MakeNotice</c>'s notable-item floor (Server/ts25zone/S04_MyWork02.cpp:483-485).</summary>
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

    /// <summary>
    ///     <see cref="ShouldBroadcast" />: fire the cluster-wide elite-notice for this reward.
    ///     <see cref="WriteEliteGainAudit" />: additionally write the 1035/1036/1037 gain-item audit entry
    ///     (only ever true for those three boxes, when the reward is elite-typed).
    /// </summary>
    public readonly record struct NoticeDecision(bool ShouldBroadcast, bool WriteEliteGainAudit);
}
