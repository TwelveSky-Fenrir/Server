using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeLockService(
    TradeRegistry trades,
    ICharacterRepository characters,
    ILogger<TradeLockService> logger) : ITradeLockService
{
    public TradeLockAttempt TryLock(int characterId)
    {
        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
        {
            logger.LogDebug("Trade lock ignored: character {CharacterId} has no active trade session",
                characterId);
            return new TradeLockAttempt(false, null);
        }

        var side = trade.SideOf(characterId);
        if (side.MenuState >= 2)
        {
            logger.LogDebug("Trade lock ignored: character {CharacterId} already fully confirmed", characterId);
            return new TradeLockAttempt(false, null);
        }

        side.MenuState++;
        logger.LogDebug("Trade lock notch: character {CharacterId} menu state now {MenuState}", characterId,
            side.MenuState);
        return new TradeLockAttempt(true, trade);
    }

    public async ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA,
        PlayerRuntimeState playerB, Zone zoneB, int characterId, CancellationToken cancellationToken)
    {
        var planA = TradeCommitPlanner.BuildFinalContainers(
            playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
            playerA.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
            trade.SideA.Slots, trade.SideB.Slots);

        var planB = TradeCommitPlanner.BuildFinalContainers(
            playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
            playerB.Inventory.GetContainer(ContainerMatrix.InventoryPage1),
            trade.SideB.Slots, trade.SideA.Slots);

        if (planA.Overflowed || planB.Overflowed)
        {
            // No wire error code for overflow: reset both menus to locked (not cleared) so players can retry.
            logger.LogInformation(
                "Trade commit rejected: character {PlayerAId}/{PlayerBId} would overflow inventory (side A overflowed {PlanAOverflowed}, side B overflowed {PlanBOverflowed}) -- both menus reset to locked for retry",
                trade.PlayerAId, trade.PlayerBId, planA.Overflowed, planB.Overflowed);
            trade.SideA.MenuState = 1;
            trade.SideB.MenuState = 1;
            return;
        }

        var moneyDeltaA = trade.SideB.Money - trade.SideA.Money;
        var bigMoneyDeltaA = trade.SideB.BigMoney - trade.SideA.BigMoney;
        var moneyDeltaB = trade.SideA.Money - trade.SideB.Money;
        var bigMoneyDeltaB = trade.SideA.BigMoney - trade.SideB.BigMoney;

        try
        {
            await characters.ExecuteTradeAsync(
                trade.PlayerAId, ToTvps(planA.Page0), ToTvps(planA.Page1), moneyDeltaA, bigMoneyDeltaA,
                trade.PlayerBId, ToTvps(planB.Page0), ToTvps(planB.Page1), moneyDeltaB, bigMoneyDeltaB,
                cancellationToken,
                ToTradedTvps(trade.SideA), ToTradedTvps(trade.SideB),
                trade.SideA.Money, trade.SideA.BigMoney, trade.SideB.Money, trade.SideB.BigMoney);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Trade commit failed: character {PlayerAId}/{PlayerBId} ExecuteTradeAsync threw -- both sessions will be torn down",
                trade.PlayerAId, trade.PlayerBId);
            throw;
        }

        await PostMirrorAndWaitAsync(zoneA, playerA.CharacterId, planA, cancellationToken);
        await PostMirrorAndWaitAsync(zoneB, playerB.CharacterId, planB, cancellationToken);

        trades.TryEnd(characterId, out _);

        logger.LogInformation(
            "Trade committed: character {PlayerAId} money delta {MoneyDeltaA}/{BigMoneyDeltaA} ({ItemsReceivedByA} items received), " +
            "character {PlayerBId} money delta {MoneyDeltaB}/{BigMoneyDeltaB} ({ItemsReceivedByB} items received)",
            trade.PlayerAId, moneyDeltaA, bigMoneyDeltaA, trade.SideB.Slots.Count(s => s is not null),
            trade.PlayerBId, moneyDeltaB, bigMoneyDeltaB, trade.SideA.Slots.Count(s => s is not null));

        var result = new TradeEndResponse { Result = 0 };
        playerA.Session.Send(result);
        playerB.Session.Send(result);
    }

    private static async Task PostMirrorAndWaitAsync(Zone zone, int characterId, TradeCommitPlanner.Plan plan,
        CancellationToken cancellationToken)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, plan.Page0),
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, plan.Page1));

        await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
            cancellationToken);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    /// <summary>
    ///     A side's own contributed trade-window offer (GL_615_TRADE_ITEM2 audit rows) -- one row per occupied
    ///     escrow slot, keyed by trade-offer slot index (0-7), not by the source inventory container/slot the
    ///     item came from. usp_CharacterTrade_Execute only reads the item-identity columns off this TVP for
    ///     logging, never the Slot column itself, so the exact index carries no functional meaning beyond
    ///     matching TradeOfferCodec's own per-slot iteration order.
    /// </summary>
    private static List<CharacterItemSlotTvp> ToTradedTvps(TradeOfferSide side)
    {
        var list = new List<CharacterItemSlotTvp>(TradeLimits.SlotCount);
        for (byte i = 0; i < TradeLimits.SlotCount; i++)
        {
            if (side.Slots[i] is not { } slot)
                continue;
            list.Add(slot.Stack.ToTvp(i));
        }

        return list;
    }
}
