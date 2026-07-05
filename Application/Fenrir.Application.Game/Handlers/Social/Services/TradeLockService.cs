using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Result of a menu-lock attempt: whether this call advanced this side's own menu state.</summary>
public readonly record struct TradeLockAttempt(bool Locked, TradeSession? Trade);

public interface ITradeLockService
{
    /// <summary>2-notch confirm: first call locks (menu 0-&gt;1), second confirms (1-&gt;2).</summary>
    TradeLockAttempt TryLock(int characterId);

    /// <summary>
    ///     Commits atomically (<see cref="ICharacterRepository.ExecuteTradeAsync" />) and mirrors each side's
    ///     new container back to their own zone. An overflow aborts the whole commit -- no partial state.
    /// </summary>
    ValueTask CommitAsync(TradeSession trade, PlayerRuntimeState playerA, Zone zoneA, PlayerRuntimeState playerB,
        Zone zoneB, int characterId, CancellationToken cancellationToken);
}

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
