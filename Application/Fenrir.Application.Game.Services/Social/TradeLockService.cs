using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeLockService(TradeRegistry trades, ICharacterRepository characters) : ITradeLockService
{
    public TradeLockAttempt TryLock(int characterId)
    {
        if (!trades.TryGetSession(characterId, out var trade) || trade is null)
            return new TradeLockAttempt(false, null);

        var side = trade.SideOf(characterId);
        if (side.MenuState >= 2)
            return new TradeLockAttempt(false, null);

        side.MenuState++;
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
            trade.SideA.MenuState = 1;
            trade.SideB.MenuState = 1;
            return;
        }

        await characters.ExecuteTradeAsync(
            trade.PlayerAId, ToTvps(planA.Page0), ToTvps(planA.Page1),
            trade.SideB.Money - trade.SideA.Money, trade.SideB.BigMoney - trade.SideA.BigMoney,
            trade.PlayerBId, ToTvps(planB.Page0), ToTvps(planB.Page1),
            trade.SideA.Money - trade.SideB.Money, trade.SideA.BigMoney - trade.SideB.BigMoney,
            cancellationToken);

        await PostMirrorAndWaitAsync(zoneA, playerA.CharacterId, planA, cancellationToken);
        await PostMirrorAndWaitAsync(zoneB, playerB.CharacterId, planB, cancellationToken);

        trades.TryEnd(characterId, out _);

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
}
